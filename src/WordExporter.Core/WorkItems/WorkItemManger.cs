﻿using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordExporter.Core.WorkItems
{
    public class WorkItemManger
    {
        private readonly Connection _connection;
        private string _teamProjectName;

        public WorkItemManger(Connection connection)
        {
            _connection = connection;
        }

        public void SetTeamProject(String teamProjectName)
        {
            _teamProjectName = teamProjectName;
        }

        /// <summary>
        /// Load a series of Work Items for a given area and a given iteration.
        /// </summary>
        /// <param name="areaPath"></param>
        /// <param name="iterationPath"></param>
        /// <returns></returns>
        public List<WorkItem> LoadAllWorkItemForAreaAndIteration(string areaPath, string iterationPath)
        {
            StringBuilder query = new StringBuilder();
            query.AppendLine($"SELECT * FROM WorkItems Where [System.AreaPath] UNDER '{areaPath}' AND [System.IterationPath] UNDER '{iterationPath}'");
            if (!String.IsNullOrEmpty(_teamProjectName))
            {
                query.AppendLine($"AND [System.TeamProject] = '{_teamProjectName}'");
            }

            return _connection.WorkItemStore.Query(query.ToString())
                .OfType<WorkItem>()
                .ToList();
        }
    }
}