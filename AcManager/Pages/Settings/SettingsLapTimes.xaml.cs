﻿using AcManager.Tools.Profile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsLapTimes {
        public SettingsLapTimes() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            private DelegateCommand _clearCacheCommand;

            public DelegateCommand ClearCacheCommand => _clearCacheCommand ?? (_clearCacheCommand = new DelegateCommand(() => {
                LapTimesManager.Instance.ClearCache();
            }));
        }
    }
}
