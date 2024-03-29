﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using harvest_api.Models;
using harvest_api.Models.Response;
using harvest_api.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace harvest_api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HarvestController : ControllerBase
    {
        public AppDb Db { get; }
        public HarvestController(AppDb db)
        {
            Db = db;
        }

        /// <summary>
        /// Get harvest report
        /// </summary>
        /// <param name="filter">Filter by period and orchards</param>
        /// <returns>Harvest report data</returns>
        [HttpGet]
        public async Task<Response<Report>> Get([FromQuery] Filter filter)
        {
            try
            {
                var report = new Report { categories = new List<ReportCategorie>() };

                await Db.Connection.OpenAsync();
                using var command = Db.Connection.CreateCommand();
                command.CommandText = getSqlStatement(filter);
                using var reader = await command.ExecuteReaderAsync();
                while (reader.HasRows)
                {
                    while (await reader.ReadAsync())
                        report.categories.Add(new ReportCategorie
                        {
                            id = reader.GetString(0),
                            name = reader.GetString(1),
                            production = reader.GetDecimal(2),
                            cost = Math.Round(reader.GetDecimal(3), 3)
                        });
                    await reader.NextResultAsync();
                }

                return new Response<Report>(report);
            }
            catch { return new Response<Report>("error"); }
        }

        private string getSqlStatement(Filter filter)
        {
            string formatDate = "yyyy-MM-dd";
            filter.end = filter.end.AddDays(1);
            string start = filter.start.ToString(formatDate);
            string end = filter.end.ToString(formatDate);
            string categoryColumnName = string.Empty;
            string categoryColumnId = string.Empty;

            switch (filter.tab)
            {
                case ReportTabs.Varieties:
                    categoryColumnName = nameof(Harvest.varietyName);
                    categoryColumnId = nameof(Harvest.varietyName);
                    break;
                case ReportTabs.Orchards:
                    categoryColumnName = nameof(Harvest.orchardName);
                    categoryColumnId = nameof(Harvest.orchardId);
                    break;
                default:
                    return string.Empty;
            }

            var sql = $@"SELECT 
                            {categoryColumnId} as id,
                            {categoryColumnName} as category,
                            sum({ nameof(Harvest.numberOfBins)}) as production, 
                            ({nameof(Harvest.payRateByHour)} * {nameof(Harvest.hoursWorked)}) as cost
                        FROM data
                        WHERE {nameof(Harvest.pickingDate)} >= '{start}' AND {nameof(Harvest.pickingDate)} <= '{end}' ";

            if (!string.IsNullOrEmpty(filter.orchards))
            {
                var orchards = filter.orchards.Split(',').ToList();
                var orchardSql = "'" + string.Join("','", filter.orchards) + "'";
                orchardSql = orchardSql.Replace(",", "','");
                sql += $@"AND {nameof(Harvest.orchardId)} IN ({orchardSql}) ";
            }

            sql += $@"GROUP BY {categoryColumnId};";
            sql = sql.Replace("\n", string.Empty).Replace("\r", string.Empty);

            return sql;
        }
    }
}
