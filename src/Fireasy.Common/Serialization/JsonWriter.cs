﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Text;

namespace Fireasy.Common.Serialization
{
    /// <summary>
    /// 表示将对象使用json表示的编写器。
    /// </summary>
    public sealed class JsonWriter : IDisposable
    {
        private TextWriter writer;
        private bool isDisposed;
        private int level;
        private bool[] flags = new bool[3] { false, false, false };

        /// <summary>
        /// 初始化 <see cref="JsonWriter"/> 类的新实例。
        /// </summary>
        /// <param name="writer">一个 <see cref="TextWriter"/> 对象。</param>
        public JsonWriter(TextWriter writer)
        {
            this.writer = writer;
        }

        /// <summary>
        /// 获取或设置缩进的宽度。
        /// </summary>
        public int Indent { get; set; }

        /// <summary>
        /// 写入一个 null 值。
        /// </summary>
        public void WriteNull()
        {
            SetFlags(false, 0, 1, 2);

            writer.Write("null");
        }

        /// <summary>
        /// 写入一个值。
        /// </summary>
        /// <param name="value">要写入的值。</param>
        public void WriteValue(object value)
        {
            SetFlags(false, 0, 1, 2);

            writer.Write(value);
        }

        /// <summary>
        /// 写入一段 Json。
        /// </summary>
        /// <param name="json"></param>
        public void WriteRaw(string json)
        {
            writer.Write(json);
        }

        /// <summary>
        /// 写入一个键。
        /// </summary>
        /// <param name="key">要写入的键值。</param>
        public void WriteKey(string key)
        {
            SetFlags(false, 0, 1, 2);

            WriteLine();
            WriteIndent();
            writer.Write(key);
            writer.Write(JsonTokens.PairSeparator);

            if (Indent != 0)
            {
                writer.Write(' ');
            }
        }

        /// <summary>
        /// 写入一个文本值。
        /// </summary>
        /// <param name="value">要写入的值。</param>
        public void WriteString(string value)
        {
            SetFlags(false, 0, 1, 2);

            if (value == null)
            {
                WriteNull();
                return;
            }

            var sb = new StringBuilder();
            sb.Append(JsonTokens.StringDelimiter);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\r':
                        sb.Append(@"\r");
                        break;
                    case '\n':
                        sb.Append(@"\n");
                        break;
                    case '\t':
                        sb.Append(@"\t");
                        break;
                    case '\b':
                        sb.Append(@"\b");
                        break;
                    case '\f':
                        sb.Append(@"\f");
                        break;
                    case '\"':
                        sb.Append(@"\""");
                        break;
                    case '\\':
                        sb.Append(@"\\");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            sb.Append(JsonTokens.StringDelimiter);
            writer.Write(sb.ToString());
        }

        /// <summary>
        /// 写入一个逗号。
        /// </summary>
        public void WriteComma()
        {
            SetFlags(true, 1);
            SetFlags(false, 0, 2);

            writer.Write(JsonTokens.ElementSeparator);
        }

        /// <summary>
        /// 写入一个数组开始符。
        /// </summary>
        public void WriteStartArray()
        {
            if (GetFlags(0) || GetFlags(1))
            {
                WriteLine();
                WriteIndent();
            }

            SetFlags(true, 0);
            SetFlags(false, 1, 2);

            writer.Write(JsonTokens.StartArrayCharacter);
            level++;
        }

        /// <summary>
        /// 写入一个数组结束符。
        /// </summary>
        public void WriteEndArray()
        {
            level--;
            if (!GetFlags(0) && GetFlags(2))
            {
                WriteLine();
                WriteIndent();
            }

            writer.Write(JsonTokens.EndArrayCharacter);

            SetFlags(false, 0, 1, 2);
        }

        /// <summary>
        /// 写入一个对象开始符。
        /// </summary>
        public void WriteStartObject()
        {
            if (GetFlags(0) || GetFlags(1))
            {
                WriteLine();
                WriteIndent();
            }

            writer.Write(JsonTokens.StartObjectLiteralCharacter);
            level++;

            SetFlags(false, 0, 1, 2);
        }

        /// <summary>
        /// 写入一个对象结束符。
        /// </summary>
        public void WriteEndObject()
        {
            WriteLine();
            level--;
            WriteIndent();
            writer.Write(JsonTokens.EndObjectLiteralCharacter);

            SetFlags(false, 0, 1);
            SetFlags(true, 2);
        }

        /// <summary>
        /// 清理当前缓冲区，确认文本写入。
        /// </summary>
        public void Flush()
        {
            writer.Flush();
        }

        /// <summary>
        /// 释放对象所占用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// 写入缩进空格。
        /// </summary>
        protected void WriteIndent()
        {
            if (Indent != 0)
            {
                writer.Write(new string(' ', Indent * level));
            }
        }

        /// <summary>
        /// 写入换行符。
        /// </summary>
        protected void WriteLine()
        {
            if (Indent != 0)
            {
                writer.WriteLine();
            }
        }

        private void SetFlags(bool flag, params int[] bits)
        {
            if (Indent != 0)
            {
                foreach (var b in bits)
                {
                    flags[b] = flag;
                }
            }
        }

        private bool GetFlags(int bit)
        {
            return Indent != 0 ? flags[bit] : false;
        }

        /// <summary>
        /// 释放对象所占用的非托管和托管资源。
        /// </summary>
        /// <param name="disposing">为 true 则释放托管资源和非托管资源；为 false 则仅释放非托管资源。</param>
        private void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            if (disposing)
            {
                if (writer != null)
                {
                    Flush();
                    writer.Close();
                    writer = null;
                }
            }

            isDisposed = true;
        }
    }
}
