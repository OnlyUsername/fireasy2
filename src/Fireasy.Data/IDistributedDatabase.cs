﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;

namespace Fireasy.Data
{
    /// <summary>
    /// 提供分布式数据库连接支持。
    /// </summary>
    public interface IDistributedDatabase
    {
        /// <summary>
        /// 获取或设置分布式数据库连接字符串组。
        /// </summary>
        List<DistributedConnectionString> DistributedConnectionStrings { get; set; }
    }
}
