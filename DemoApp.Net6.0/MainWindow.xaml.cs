#region (c) 2022 Gilles Macabies All right reserved

// Author     : Gilles Macabies
// Solution   : FilterDataGrid
// Projet     : DemoApp.Net6.0
// File       : MainWindow.xaml.cs
// Created    : 03/12/2022
//

#endregion

using FilterDataGrid;
using Newtonsoft.Json;
using SharedModelView.ModelView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Newtonsoft.Json;
using DataGridTextColumn = FilterDataGrid.DataGridTextColumn;

namespace DemoApp.Net6
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            AppDomain.CurrentDomain.FirstChanceException += (source, e) =>
            {
                Debug.WriteLine("FirstChanceException event raised in " +
                                $"{AppDomain.CurrentDomain.FriendlyName}: {e.Exception.Message} {source}");
            };
#endif
            DataContext = new ModelView();

            // if you want to change column order and column names (AutoGenerateColumns="True")
        }

        private async void Button_Write_Click(object sender, RoutedEventArgs e)
        {
            string filePath = "D:\\test\\filterpreset.json";

            var filterPreset = FilterDataGrid.FilterPreset;
            var json = JsonConvert.SerializeObject(filterPreset, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            File.WriteAllText(filePath, json);

            MessageBox.Show("Succeeded!");
        }

        private async void Button_Read_Click(object sender, RoutedEventArgs e)
        {
            FilterDataGrid.FilterPreset = new List<FilterCommon>();
            await Task.Delay(5_000);

            string filePath = "D:\\test\\filterpreset.json";
            var json = File.ReadAllText(filePath);
            var filterPreset = JsonConvert.DeserializeObject<List<FilterCommon>>(json);

            FilterDataGrid.FilterPreset = filterPreset;
        }

        private async void Button_MemorySet_Click(object sender, RoutedEventArgs e)
        {

            FilterDataGrid.IncludeFields = "FirstName,Age";
            FilterDataGridAuto.IncludeFields = "FirstName,Age";


            await Task.Delay(5_000);

            FilterDataGrid.IncludeFields = "*";
            FilterDataGridAuto.IncludeFields = "*";

            //var filterPreset = FilterDataGrid.FilterPreset.ToList();

            //FilterDataGrid.FilterPreset = new List<FilterCommon>();
            //await Task.Delay(5_000);

            //FilterDataGrid.FilterPreset = filterPreset;

        }
    }
}