﻿using Fireasy.Common;
using System;
using System.Data;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Fireasy.Data.Entity.Linq.Expressions
{
    [DebuggerDisplay("DbNodeType={DbNodeType},Name={Name},Value={Value},DataType={DataType}")]
    public class NamedValueExpression : DbExpression
    {
        public NamedValueExpression(string name, Expression value, DbType dbType = DbType.String)
            : base(DbExpressionType.NamedValue, value.Type)
        {
            Guard.ArgumentNull(name, nameof(name));
            Guard.ArgumentNull(value, nameof(value));

            Name = name;
            Value = value;
            DataType = dbType;
        }

        public string Name { get; private set; }

        public Expression Value { get; private set; }

        public DbType DataType { get; private set; }
    }
}
