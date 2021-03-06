﻿using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;
using WordExporter.Core.Support;
using WordExporter.Core.WordManipulation;
using WordExporter.Core.WorkItems;

namespace WordExporter.Core.Templates.Parser
{
    public sealed class QuerySection : Section
    {
        private QuerySection(IEnumerable<KeyValue> keyValuePairList)
        {
            Query = keyValuePairList.GetStringValue("query");
            foreach (var templateKeys in keyValuePairList.Where(k => k.Key
                .StartsWith("template/"))
                .Select(k => new
                {
                    realKey = k.Key.Substring("template/".Length),
                    value = k.Value
                }))
            {
                SpecificTemplates[templateKeys.realKey] = templateKeys.value;
            }
            TableTemplate = keyValuePairList.GetStringValue("tableTemplate");
            Limit = keyValuePairList.GetIntValue("limit", Int32.MaxValue);
            QueryParameters = new List<Dictionary<string, string>>();
            RepeatForEachIteration = keyValuePairList.GetBooleanValue("repeatForEachIteration");
            foreach (var parameter in keyValuePairList.Where(kvl => kvl.Key == "parameterSet"))
            {
                var set = ConfigurationParser.ParameterSetList.Parse(parameter.Value);

                var dictionary = set.ToDictionary(k => k.Key, v => v.Value);
                QueryParameters.Add(dictionary);
            }
        }

        public String Query { get; private set; }

        /// <summary>
        /// If this property is set we have a special export where we create a table of 
        /// work items.
        /// </summary>
        public String TableTemplate { get; set; }
        public Int32 Limit { get; set; }

        /// <summary>
        /// <para>
        /// Query can be parametric, this means that we should execute query for each 
        /// series of parameters.
        /// </para>
        /// <para>
        /// If there are no parameters, the query will be executed just one time with 
        /// standard set of parameter.
        /// </para>
        /// </summary>
        public List<Dictionary<String, String>> QueryParameters { get; private set; }

        private readonly Dictionary<String, String> SpecificTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool RepeatForEachIteration { get; private set; }

        public String GetTemplateForWorkItem(string workItemTypeName)
        {
            return SpecificTemplates.TryGetValue(workItemTypeName);
        }

        #region syntax

        public static Parser<QuerySection> Parser =
          from keyValueList in ConfigurationParser.KeyValueList
          select new QuerySection(keyValueList);

        #endregion

        public override void Assemble(
            WordManipulator manipulator,
            Dictionary<string, object> parameters,
            ConnectionManager connectionManager,
            WordTemplateFolderManager wordTemplateFolderManager,
            string teamProjectName)
        {
            parameters = parameters.ToDictionary(k => k.Key, v => v.Value); //clone
            //If we do not have query parameters we have a single query or we can have parametrized query with iterationPath
            var queries = PrepareQueries(parameters);
            WorkItemManger workItemManger = new WorkItemManger(connectionManager);
            workItemManger.SetTeamProject(teamProjectName);

            foreach (var query in queries)
            {
                var workItems = workItemManger.ExecuteQuery(query).Take(Limit);

                if (String.IsNullOrEmpty(TableTemplate))
                {
                    foreach (var workItem in workItems)
                    {
                        if (!SpecificTemplates.TryGetValue(workItem.Type.Name, out var templateName))
                        {
                            templateName = wordTemplateFolderManager.GetTemplateFor(workItem.Type.Name);
                        }
                        else
                        {
                            templateName = wordTemplateFolderManager.GenerateFullFileName(templateName);
                        }

                        manipulator.InsertWorkItem(workItem, templateName, true, parameters);
                    }
                }
                else
                {
                    //We have a table template, we want to export work item as a list 
                    var tableFile = wordTemplateFolderManager.GenerateFullFileName(TableTemplate);
                    var tempFile = wordTemplateFolderManager.CopyFileInTempDirectory(tableFile);
                    using (var tableManipulator = new WordManipulator(tempFile, false))
                    {
                        tableManipulator.SubstituteTokens(parameters);
                        tableManipulator.FillTableWithCompositeWorkItems(true, workItems);
                    }
                    manipulator.AppendOtherWordFile(tempFile);
                }
            }

            base.Assemble(manipulator, parameters, connectionManager, wordTemplateFolderManager, teamProjectName);
        }

        private List<String> PrepareQueries(Dictionary<string, object> parameters)
        {
            var retValue = new List<String>();
            if (QueryParameters.Count == 0)
            {
                if (!RepeatForEachIteration)
                {
                    retValue.Add(SubstituteParametersInQuery(Query, parameters));
                }
                else
                {
                    if (parameters.TryGetValue("iterations", out object iterations)
                        && iterations is List<String> iterationList)
                    {
                        foreach (var iteration in iterationList)
                        {
                            var query = Query.Replace("{iterationPath}", iteration);
                            retValue.Add(SubstituteParametersInQuery(query, parameters));
                        }
                    }
                    else
                    {
                        //By convention we should have a list of valid iterations names inside parameters dictionary.
                        //TODO: handle the error.
                    }
                }
            }
            else
            {
                foreach (var parameterSet in QueryParameters)
                {
                    var query = Query;
                    foreach (var parameter in parameterSet)
                    {
                        query = query.Replace('{' + parameter.Key + '}', parameter.Value);
                        parameters[parameter.Key] = parameter.Value;
                    }
                    retValue.Add(SubstituteParametersInQuery(query, parameters));
                }
            }

            return retValue;
        }

        private string SubstituteParametersInQuery(string query, Dictionary<string, object> parameters)
        {
            foreach (var parameter in parameters)
            {
                var value = parameter.Value?.ToString() ?? "";
                query = query.Replace('{' + parameter.Key + '}', value);
            }
            return query;
        }
    }
}
