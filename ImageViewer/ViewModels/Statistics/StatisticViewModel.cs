﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ImageFramework.Annotations;
using ImageFramework.Model;
using ImageFramework.Model.Statistics;
using ImageViewer.Models;

namespace ImageViewer.ViewModels.Statistics
{
    public class StatisticViewModel : INotifyPropertyChanged
    {
        private readonly int index;
        private readonly ModelsEx models;
        private readonly StatisticModel model;
        private readonly ImagePipeline pipe;
        private readonly StatisticsViewModel viewModel;

        public StatisticViewModel(int index, ModelsEx models, StatisticsViewModel viewModel)
        {
            this.index = index;
            this.models = models;
            this.viewModel = viewModel;
            this.model = models.Statistics[index];
            this.pipe = models.Pipelines[index];
            Name = "Equation " + (index + 1);

            viewModel.PropertyChanged += ViewModelOnPropertyChanged;
            model.PropertyChanged += ModelOnPropertyChanged;
            pipe.PropertyChanged += PipeOnPropertyChanged;
        }

        public string Name { get; }

        private void PipeOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ImagePipeline.IsEnabled):
                    OnPropertyChanged(nameof(Visibility));
                    break;
            }
        }

        private void ModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(StatisticModel.Stats):
                    if (viewModel.IsVisible)
                    {
                        Update();
                    }
                    break;
            }
        }

        private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(StatisticsViewModel.IsVisible):
                case nameof(StatisticsViewModel.SelectedChannel):
                    if (viewModel.IsVisible)
                    {
                        Update();
                    }
                    break;
            }
        }

        private void Update()
        {
            OnPropertyChanged(nameof(Average));
            OnPropertyChanged(nameof(Min));
            OnPropertyChanged(nameof(Max));
            OnPropertyChanged(nameof(RootAverage));
        }

        public Visibility Visibility => models.Pipelines[index].IsEnabled ? Visibility.Visible : Visibility.Collapsed;

        public string Average
        {
            get => model.Stats.Get((DefaultStatistics.Types)viewModel.SelectedChannel.Cargo, DefaultStatistics.Metrics.Avg).ToString(ImageFramework.Model.Models.Culture);
            set { }
        }

        public string RootAverage
        {
            get => Math.Sqrt(model.Stats.Get((DefaultStatistics.Types)viewModel.SelectedChannel.Cargo, DefaultStatistics.Metrics.Avg)).ToString(ImageFramework.Model.Models.Culture);
            set { }
        }

        public string Min
        {
            get => model.Stats.Get((DefaultStatistics.Types)viewModel.SelectedChannel.Cargo, DefaultStatistics.Metrics.Min).ToString(ImageFramework.Model.Models.Culture);
            set { }
        }

        public string Max
        {
            get => model.Stats.Get((DefaultStatistics.Types)viewModel.SelectedChannel.Cargo, DefaultStatistics.Metrics.Max).ToString(ImageFramework.Model.Models.Culture);
            set { }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
