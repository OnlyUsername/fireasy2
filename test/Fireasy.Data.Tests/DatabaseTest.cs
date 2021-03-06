﻿using Fireasy.Common.Configuration;
using Fireasy.Data.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Fireasy.Data.Tests
{
    [TestClass]
    public class DatabaseTest
    {
        public DatabaseTest()
        {
            InitConfig.Init();
        }

        [TestMethod]
        public void TestExecuteNonQuery()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var parameters = new ParameterCollection();
                parameters.Add("city", "__");
                var result = db.ExecuteNonQuery((SqlCommand)"delete from customers where city = @city", parameters);
                Console.WriteLine($"执行完毕 结果为{result}");
            }
        }

        [TestMethod]
        public void TestExecuteNonQueryAsync()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var parameters = new ParameterCollection();
                parameters.Add("city", "London111");
                var task = db.ExecuteNonQueryAsync((SqlCommand)"delete from customers where city = @city", parameters);
                task.ContinueWith(t =>
                    {
                        Console.WriteLine($"执行完毕 结果为{t.Result}");
                    });

                Console.WriteLine("后续代码");
                DoSomthings();
            }
        }

        [TestMethod]
        public void TestFillDataSet()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var ds = new DataSet();
                var paper = new DataPager(5, 0);
                var parameters = new ParameterCollection();
                parameters.Add("city", "London");
                db.FillDataSet(ds, (SqlCommand)"select city from customers where city <> @city", segment: paper, parameters: parameters);
                Assert.AreEqual(5, ds.Tables[0].Rows.Count);
            }
        }

        [TestMethod]
        public void TestExecuteScalr()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var parameters = new ParameterCollection();
                parameters.Add("city", "London");
                var result = db.ExecuteScalar<string>((SqlCommand)"select city from customers where city = @city and city <> ?city", parameters);
                Console.WriteLine($"执行完毕 结果为{result}");
            }
        }

        [TestMethod]
        public void TestExecuteScalrWithParameters()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var parameters = new ParameterCollection(new { city = "London" });
                var result = db.ExecuteScalar<string>((SqlCommand)"select city from customers where city = @city and city <> @city", parameters);
                Console.WriteLine($"执行完毕 结果为{result}");
            }
        }

        [TestMethod]
        public void TestExecuteScalrAsync()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var parameters = new ParameterCollection();
                parameters.Add("city", "Berlin");
                var task = db.ExecuteScalarAsync<string>((SqlCommand)"select city from customers where city = @city", parameters);
                task.ContinueWith(t =>
                    {
                        Console.WriteLine($"执行完毕 结果为{t.Result}");
                    });

                Console.WriteLine("后续代码");
                DoSomthings();
            }
        }

        private void DoSomthings()
        {
            Console.WriteLine("后续代码");
            Console.WriteLine("后续代码");
            Console.WriteLine("后续代码");
            Console.WriteLine("后续代码");
        }

        [TestMethod]
        public void TestExecuteReader()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var paper = new DataPager(2, 0);
                var parameters = new ParameterCollection();
                parameters.Add("city", "London");
                using (var reader = db.ExecuteReader((SqlCommand)"select city from customers where city <> @city", paper, parameters))
                {
                    while (reader.Read())
                    {
                        Console.WriteLine(reader.GetValue(0));
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestExecuteReaderAsync()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var paper = new DataPager(2, 0);
                var parameters = new ParameterCollection();
                parameters.Add("city", "London");
                using (var reader = await db.ExecuteReaderAsync((SqlCommand)"select city from customers where city <> @city", paper, parameters))
                {
                    while (reader.Read())
                    {
                        Console.WriteLine(reader.GetValue(0));
                    }
                }

                Console.WriteLine("后续代码");
            }
        }

        [TestMethod]
        public void ExecuteDataTableByPage()
        {
            using (var database = DatabaseFactory.CreateDatabase())
            {
                var sql = new SqlCommand("SELECT * FROM Customers");
                var pager = new DataPager(5, 2);
                var table = database.ExecuteDataTable(sql, segment: pager);
                table.EachRow((r, i) => Console.WriteLine("CustomerID: {0}", r[0]));
            };
        }

        [TestMethod]
        public void TestExecuteEnumerable()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var result = db.ExecuteEnumerable<Customer>((SqlCommand)"select * from customers");
                Console.WriteLine($"执行完毕 结果为{result.Count()}");
            }
        }

        [TestMethod]
        public void TestExecuteDynamicEnumerable()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var pager = new DataPager(5, 0);
                var result = db.ExecuteEnumerable((SqlCommand)"select * from customers").ToList();
                Console.WriteLine($"执行完毕 结果为{result.Count()}");
            }
        }

        [TestMethod]
        public void TestExecuteEnumerableByPager()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var pager = new DataPager(5, 0);
                var result = db.ExecuteEnumerable<Customer>((SqlCommand)"select * from customers", pager);
                Console.WriteLine($"执行完毕 结果为{result.Count()}");
            }
        }

        [TestMethod]
        public void TestExecuteEnumerableByExpiration()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var sql = (SqlCommand)"select * from customers";

                var pager = new DataPager(5, 0);

                //查询总记录数，同时采用20秒缓存，因此可以不用每次都查询总记录数
                pager.Evaluator = new TotalRecordEvaluator
                {
                    Expiration = TimeSpan.FromSeconds(20)
                };

                while (true)
                {
                    var result = db.ExecuteEnumerable<Customer>(sql, pager);
                    Console.WriteLine($"执行完毕 结果为{result.Count()}");

                    if (pager.CurrentPageIndex + 1 >= pager.PageCount)
                    {
                        break;
                    }

                    pager.CurrentPageIndex++;
                }
            }
        }

        [TestMethod]
        public void TestExecuteEnumerableByTryNextEvaluator()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var sql = (SqlCommand)"select * from customers";

                //每页5条
                var pager = new DataPager(5, 0);
                pager.Evaluator = new TryNextEvaluator();

                //记录当前查询出的记录数
                var recordCount = 0;

                while (true)
                {
                    var result = db.ExecuteEnumerable<Customer>(sql, pager);
                    Console.WriteLine($"执行完毕 结果为{result.Count()}");

                    //判断是否到最后一页
                    if (recordCount + pager.PageSize > pager.RecordCount)
                    {
                        break;
                    }

                    //赋值新记录数
                    recordCount = pager.RecordCount;
                    pager.CurrentPageIndex++;
                }
            }
        }

        [TestMethod]
        public void TestExecuteEnumerableAsync()
        {
            using (var db = DatabaseFactory.CreateDatabase())
            {
                var paper = new DataPager(2, 0);
                var task = db.ExecuteEnumerableAsync<Customer>((SqlCommand)"select * from customers", paper);
                task.ContinueWith(t =>
                {
                    Console.WriteLine($"执行完毕 结果为{t.Result.Count()}");
                });

                Console.WriteLine("后续代码");
            }
        }

        public class Customer
        {
            public string CustomerId { get; set; }

            public string City { get; set; }
        }
    }
}
