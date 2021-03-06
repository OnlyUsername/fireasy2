﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Fireasy.Common;
using Fireasy.Common.Emit;
using Fireasy.Common.Extensions;
using Fireasy.Data.Entity.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Fireasy.Data.Entity.Dynamic
{
    /// <summary>
    /// 提供动态构造实体的生成器。无法继承此类。
    /// </summary>
    public sealed class EntityTypeBuilder
    {
        private static readonly MethodInfo GetValueMethod = typeof(EntityObject).GetMethods().FirstOrDefault(s => s.Name == nameof(IEntity.GetValue));
        private static readonly MethodInfo SetValueMethod = typeof(EntityObject).GetMethods().FirstOrDefault(s => s.Name == nameof(IEntity.SetValue));
        private static readonly MethodInfo RegisterMethod = typeof(PropertyUnity).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(s => s.Name == nameof(PropertyUnity.RegisterProperty) && !s.IsGenericMethod && s.GetParameters().Length == 2);
        private static readonly MethodInfo TypeGetTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo SetReferenceMethod = typeof(IPropertyReference).GetMethod($"set_{nameof(IPropertyReference.Reference)}");
        private static readonly MethodInfo GetPropertyMethod = typeof(EntityTypeBuilder).GetMethod(nameof(EntityTypeBuilder.GetCachedProperty), BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo DelegateInvokeMethod = typeof(Delegate).GetMethod(nameof(Delegate.DynamicInvoke));

        private readonly DynamicAssemblyBuilder assemblyBuilder;
        private List<DynamicFieldBuilder> fields;
        private Dictionary<IProperty, List<Expression<Func<Attribute>>>> validations;
        private List<Expression<Func<Attribute>>> eValidations;

        private static ConcurrentDictionary<string, Dictionary<string, Delegate>> cache = new ConcurrentDictionary<string, Dictionary<string, Delegate>>();

        /// <summary>
        /// 初始化 <see cref="EntityTypeBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="typeName">动态类的类型名称。</param>
        /// <param name="assemblyBuilder">一个 <see cref="DynamicAssemblyBuilder"/> 容器。</param>
        /// <param name="baseType">所要继承的抽象类型，默认为 <see cref="EntityObject"/> 类。</param>
        public EntityTypeBuilder(string typeName, DynamicAssemblyBuilder assemblyBuilder = null, Type baseType = null)
        {
            TypeName = typeName;
            this.assemblyBuilder = assemblyBuilder ?? new DynamicAssemblyBuilder("<DynamicType>_" + typeName);
            InnerBuilder = this.assemblyBuilder.DefineType(TypeName, baseType: baseType ?? typeof(EntityObject));
            EntityType = InnerBuilder.UnderlyingSystemType;
            Properties = new List<IProperty>();
        }

        /// <summary>
        /// 获取实体类的名称。
        /// </summary>
        public string TypeName { get; private set; }

        /// <summary>
        /// 获取或设置映射对象。
        /// </summary>
        public EntityMappingAttribute Mapping { get; set; }

        /// <summary>
        /// 获取或设置包含的属性。
        /// </summary>
        public List<IProperty> Properties { get; set; }

        /// <summary>
        /// 获取实体类型。
        /// </summary>
        public Type EntityType { get; private set; }

        /// <summary>
        /// 获取 <see cref="DynamicTypeBuilder"/> 对象。
        /// </summary>
        public DynamicTypeBuilder InnerBuilder { get; private set; }

        /// <summary>
        /// 为属性添加验证规则。
        /// </summary>
        /// <param name="property">要验证的属性。</param>
        /// <param name="attributes">一组 <see cref="ValidationAttribute"/> 特性。</param>
        public void DefineValidateRule(IProperty property, params Expression<Func<Attribute>>[] attributes)
        {
            Guard.ArgumentNull(attributes, nameof(attributes));
            if (validations == null)
            {
                validations = new Dictionary<IProperty, List<Expression<Func<Attribute>>>>();
            }

            var list = validations.TryGetValue(property, () => new List<Expression<Func<Attribute>>>());
            list.AddRange(attributes);
        }

        /// <summary>
        /// 为实体添加验证规则。
        /// </summary>
        /// <param name="attributes">一组 <see cref="ValidationAttribute"/> 特性。</param>
        public void DefineValidateRule(params Expression<Func<Attribute>>[] attributes)
        {
            Guard.ArgumentNull(attributes, nameof(attributes));
            if (eValidations == null)
            {
                eValidations = new List<Expression<Func<Attribute>>>();
            }

            eValidations.AddRange(attributes);
        }

        /// <summary>
        /// 实现多个接口类型。
        /// </summary>
        /// <param name="interfaceTypes"></param>
        public void ImplementInterfaces(params Type[] interfaceTypes)
        {
            if (interfaceTypes != null)
            {
                foreach (var type in interfaceTypes)
                {
                    InnerBuilder.ImplementInterface(type);
                }
            }
        }

        /// <summary>
        /// 为动态类添加特性。
        /// </summary>
        /// <param name="expression"></param>
        public void SetCustomAttribute(Expression<Func<Attribute>> expression)
        {
            InnerBuilder.SetCustomAttribute(expression);
        }

        /// <summary>
        /// 创建一个动态的实体类型。
        /// </summary>
        /// <returns></returns>
        public Type Create()
        {
            if (Mapping != null)
            {
                InnerBuilder.SetCustomAttribute<EntityMappingAttribute>(Mapping.TableName);
            }

            fields = new List<DynamicFieldBuilder>();

            DefineProperties(fields);
            DefineConstructors(fields);

            InnerBuilder.SetCustomAttribute(() => new DescriptionAttribute("dfadf"));

            return InnerBuilder.CreateType();
        }

        public static IProperty GetCachedProperty(string key)
        {
            var k = key.Split(':');
            if (cache.TryGetValue(k[0], out Dictionary<string, Delegate> list))
            {
                return list[k[1]].DynamicInvoke() as IProperty;
            }

            return null;
        }

        private string GetFieldName(IProperty property)
        {
            return "<>__" + property.Name;
        }

        private void DefineProperties(ICollection<DynamicFieldBuilder> fields)
        {
            if (Properties == null)
            {
                return;
            }

            var dict = cache.GetOrAdd($"{assemblyBuilder.AssemblyName}-{TypeName}", k => new Dictionary<string, Delegate>());

            foreach (var property in Properties)
            {
                dict.Add(property.Name, MakeLambdaExpression(property).Compile());

                var fieldBuilder = InnerBuilder.DefineField(GetFieldName(property), typeof(IProperty), null, VisualDecoration.Public, CallingDecoration.Static);
                var propertyBuider = InnerBuilder.DefineProperty(property.Name, property.Type, VisualDecoration.Public, CallingDecoration.Virtual);
                var isEnum = property.Type.IsEnum;
                var opType = isEnum ? typeof(Enum) : property.Type;

                var op_Explicit = typeof(PropertyValue).GetMethods().FirstOrDefault(s => s.Name == "op_Explicit" && s.ReturnType == property.Type);
                var op_Implicit = typeof(PropertyValue).GetMethods().FirstOrDefault(s => s.Name == "op_Implicit" && s.GetParameters()[0].ParameterType == property.Type);

                var getMethod = propertyBuider.DefineGetMethod(calling: CallingDecoration.Virtual, ilCoding: bc =>
                    {
                        bc.Emitter.DeclareLocal(property.Type);
                        bc.Emitter
                            .ldarg_0
                            .ldsfld(fieldBuilder.FieldBuilder)
                            .callvirt(GetValueMethod)
                            .Assert(op_Explicit != null, e1 => e1.call(op_Explicit))
                            .Assert(isEnum, e1 => e1.unbox_any(opType))
                            .stloc_0
                            .ldloc_0
                            .ret();
                    });

                var setMethod = propertyBuider.DefineSetMethod(calling: CallingDecoration.Virtual, ilCoding: bc =>
                    {
                        bc.Emitter
                            .ldarg_0
                            .ldsfld(fieldBuilder.FieldBuilder)
                            .ldarg_1
                            .Assert(isEnum, e1 => e1.box(property.Type))
                            .Assert(op_Implicit != null, e1 => e1.call(op_Implicit))
                            .callvirt(SetValueMethod)
                            .ret();
                    });

                setMethod.DefineParameter("value");

                if (validations != null && validations.TryGetValue(property, out List<Expression<Func<Attribute>>> attribues))
                {
                    attribues.ForEach(s => propertyBuider.SetCustomAttribute(s));
                }

                fields.Add(fieldBuilder);
            }
        }

        private void DefineConstructors(IList<DynamicFieldBuilder> fields)
        {
            InnerBuilder.DefineConstructor(null);
            if (Properties != null)
            {
                InnerBuilder.DefineConstructor(null, ilCoding: bc =>
                    bc.Emitter.For(0, Properties.Count, (e, i) =>
                        RegisterProperty(fields[i], e, Properties[i]))
                        .ret(),
                    calling: CallingDecoration.Static);
            }
        }

        private EmitHelper RegisterProperty(DynamicFieldBuilder fieldBuilder, EmitHelper emiter, IProperty property)
        {
            var local = emiter.DeclareLocal(property.GetType());
            return emiter
                .ldtoken(InnerBuilder.UnderlyingSystemType)
                .call(TypeGetTypeFromHandle)
                .ldstr($"{assemblyBuilder.AssemblyName}-{TypeName}:{property.Name}")
                .call(GetPropertyMethod)
                .stloc(local)
                .Assert(
                    property is IPropertyReference,
                    e =>
                    {
                        var p = property.As<IPropertyReference>().Reference;
                        var dynamicFieldBuilder = fields.FirstOrDefault(s => s.FieldName == GetFieldName(p));
                        if (dynamicFieldBuilder != null)
                        {
                            var field = Properties.Contains(p) ? dynamicFieldBuilder.FieldBuilder :
                                p.EntityType.GetFields(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(s => s.Name == GetFieldName(p));
                            if (field != null)
                            {
                                e.ldloc(local).ldsfld(field).call(SetReferenceMethod);
                            }
                        }
                    })
                .ldloc(local)
                .call(RegisterMethod)
                .stsfld(fieldBuilder.FieldBuilder);
        }

        /// <summary>
        /// 创建一个新对象并初始化的表达式。
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private Expression MakeMemberInitExpression(object obj)
        {
            var newExp = Expression.New(obj.GetType());
            var binds = new List<MemberBinding>();

            obj.Compare((pro, value) =>
                {
                    if (value is IProperty)
                    {
                        return;
                    }

                    if (value is PropertyValue propertyValue)
                    {
                        binds.Add(Expression.Bind(pro, Expression.Convert(Expression.Constant(propertyValue.GetValue()), typeof(PropertyValue))));
                    }
                    else if (pro.PropertyType.IsValueType || pro.PropertyType == typeof(string))
                    {
                        binds.Add(pro.PropertyType.IsNullableType()
                            ? Expression.Bind(pro, Expression.Convert(Expression.Constant(value), pro.PropertyType))
                            : Expression.Bind(pro, Expression.Constant(value)));
                    }
                    else if (pro.PropertyType == typeof(Type))
                    {
                        binds.Add(Expression.Bind(pro, Expression.Constant(value, typeof(Type))));
                    }
                    else
                    {
                        binds.Add(Expression.Bind(pro, MakeMemberInitExpression(value)));
                    }
                });

            return Expression.MemberInit(newExp, binds);
        }

        /// <summary>
        /// 根据一个 <see cref="IProperty"/> 创建一个 <see cref="LambdaExpression"/>。
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private LambdaExpression MakeLambdaExpression(IProperty property)
        {
            return Expression.Lambda<Func<IProperty>>(MakeMemberInitExpression(property));
        }
    }
}