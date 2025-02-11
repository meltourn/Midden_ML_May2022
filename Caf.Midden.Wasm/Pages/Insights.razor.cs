﻿using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntDesign.Charts;
using Caf.Midden.Core.Models.v0_1;
using System.Globalization;

namespace Caf.Midden.Wasm.Pages
{
    public partial class Insights : IDisposable
    {
        // Simple stats
        int TotalDatasets { get; set; }
        int TotalVariables { get; set; }
        int TotalTags { get; set; }
        int TotalContacts { get; set; }
        int TotalProjects { get; set; }

        Dictionary<string, int> TopTags { get; set; }
        Dictionary<string, int> TopContacts { get; set; }

        DateTime CatalogLastUpdate { get; set; }


        IChartComponent MetadataPerZone = new Column();
        public object[] MetadataPerZoneData { get; set; }
        ColumnConfig MetadataPerZoneConfig = new ColumnConfig
        {
            Title = new Title
            {
                Visible = true,
                Text = "Datasets per Zone"
            },
            ForceFit = true,
            Padding = "auto",
            XField = "zone",
            YField = "count"
        };

        IChartComponent DatasetsOverTime = new Area();
        public object[] DatasetsOverTimeData { get; set; }
        AreaConfig DatasetsOverTimeConfig = new AreaConfig
        {
            Title = new Title
            {
                Visible = true,
                Text = "Dataset growth"
            },
            ForceFit = true,
            Padding = "auto",
            XField = "date",
            YField = "count",
            XAxis = new ValueCatTimeAxis
            {
                Visible = true,
                Type = "dateTime"
            },
            YAxis = new ValueAxis
            {
                Visible = true,
                Min = 0
            }
        };

        protected override void OnInitialized()
        {
            State.StateChanged += async (source, property)
                => await StateChanged(source, property);

            if (State?.Catalog != null)
                SetInsights();
        }

        private async Task StateChanged(
            ComponentBase source,
            string property)
        {
            if (source != this)
            {
                if (property == "UpdateCatalog")
                {
                    SetInsights();
                }

                await InvokeAsync(StateHasChanged);
            }
        }

        private void SetInsights()
        {
            if (State.Catalog == null)
                return;

            if (State.AppConfig == null)
                return;

            SetSimpleStats();
            CreateDatasetsPerZone();
            CreateDatasetsOverTime();
            SetTopStats();
        }

        private void SetSimpleStats()
        {
            if (State.Catalog == null)
                return;

            this.TotalDatasets = State.Catalog.Metadatas.Count;
            this.TotalVariables = State.Catalog.Metadatas.SelectMany(m =>
                m.Dataset.Variables).Count();

            this.CatalogLastUpdate = State.Catalog.CreationDate;

            this.TotalProjects = State.Catalog.Metadatas
                .Select(m => m.Dataset.Project)
                .Distinct()
                .Count();

            List<string> UniqueTags = new List<string>();
            List<string> UniqueContacts = new List<string>();

            foreach(Metadata meta in State.Catalog.Metadatas)
            {
                // Get unique tags from all datasets and variables
                UniqueTags = UniqueTags
                    .Union(meta.Dataset.Tags)
                    .ToList()
                    .Union(meta.Dataset.Variables
                        .SelectMany(v => v.Tags))
                    .ToList();

                // Get unique contacts from all datasets
                UniqueContacts = UniqueContacts
                    .Union<string>(meta.Dataset.Contacts
                        .Select(p => p.Name)
                        .Where(n => n != null)
                        .ToList<string>())
                    .ToList();
            }

            this.TotalTags = UniqueTags.Count;
            this.TotalContacts = UniqueContacts.Count;
        }

        private void CreateDatasetsPerZone()
        {
            List<object> objs = new List<object>();

            foreach (string zone in State.AppConfig.Zones)
            {
                int numberMetas = State.Catalog.Metadatas.Count(m =>
                    m.Dataset.Zone == zone);

                object obj = new
                {
                    zone = zone,
                    count = numberMetas
                };

                objs.Add(obj);
            }

            this.MetadataPerZoneData = objs.ToArray();

            MetadataPerZone.ChangeData(MetadataPerZoneData);
        }

        private void CreateDatasetsOverTime()
        {
            List<object> objs = new List<object>();

            // Groups datasets by creation date to get counts of those added the same month
            var grouped = State.Catalog.Metadatas.GroupBy(m => m.CreationDate.ToString("yyyyMM"))
                .Select(i => new
                {
                    date = DateTime.ParseExact(i.Key, "yyyyMM", CultureInfo.InvariantCulture),
                    count = i.Count()
                })
                .OrderBy(g => g.date)
                .ToList();

            int total = 0;

            if (grouped.Count == 0)
                return;

            DateTime min = grouped.Min(g => g.date);
            DateTime now = DateTime.UtcNow;
            DateTime curr = min;

            // Set threshold of year diff to displaying 10 years of data
            if((curr.Year - min.Year) > 10)
            {
                min = new DateTime(curr.Year - 10, 1, 1);
            }

            while(grouped.Count > 0)
            {
                object obj;

                // Moving in accending order, so curr should only match first index
                if(grouped[0].date.Month == curr.Month && grouped[0].date.Year == curr.Year)
                {
                    total += grouped[0].count;

                    grouped.RemoveAt(0);
                    
                }

                obj = new
                {
                    date = curr,
                    count = total
                };

                objs.Add(obj);

                curr = curr.AddMonths(1);

            }

            this.DatasetsOverTimeData = objs.ToArray();

            DatasetsOverTime.ChangeData(DatasetsOverTimeData);
        }
        
        private void SetTopStats()
        {
            List<string> Tags = new List<string>();
            List<string> Contacts = new List<string>();

            foreach (Metadata meta in State.Catalog.Metadatas)
            {
                // Get tags from all datasets and variables
                Tags = Tags.Concat(meta.Dataset.Tags).ToList();
                Tags = Tags.Concat(meta.Dataset.Variables
                        .SelectMany(v => v.Tags)).ToList();

                // Get contacts from all datasets
                Contacts = Contacts.Concat(meta.Dataset.Contacts
                        .Select(p => p.Name)
                        .Where(n => n != null)
                        .ToList<string>()).ToList();
            }


            this.TopTags = Tags.GroupBy(s => s)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(d => d.Value)
                .Take(5)
                .ToDictionary(d => d.Key, d => d.Value);

            this.TopContacts = Contacts.GroupBy(s => s)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(d => d.Value)
                .Take(5)
                .ToDictionary(d => d.Key, d => d.Value);
        }

        public void Dispose()
        {
            State.StateChanged -= async (source, property)
                => await StateChanged(source, property);
        }
    }
}
