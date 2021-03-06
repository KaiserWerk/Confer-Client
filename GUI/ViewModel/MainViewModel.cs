﻿using Core.Helper;
using Core.Model;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using GUI.Helper;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace GUI.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private string currentHost;
        private string currentAuthKey;
        private ObservableCollection<RemoteFileInfo> recentFiles = new ObservableCollection<RemoteFileInfo>();
        private string currentFileName;
        private string currentFileContent;
        private RemoteFileInfo selectedFile;

        public ObservableCollection<RemoteFileInfo> RecentFiles
        {
            get => recentFiles;
            set => base.Set(ref recentFiles, value);
        }

        public string CurrentFileName
        {
            get => currentFileName;
            set => base.Set(ref currentFileName, value);
        }

        public string CurrentFileContent
        {
            get => currentFileContent;
            set => base.Set(ref currentFileContent, value);
        }

        public RemoteFileInfo SelectedFile
        {
            get => selectedFile;
            set
            {
                if (value != null)
                {
                    selectedFile = value;
                    this.RequestRecentFile(value);
                }
            }
        }

        public RelayCommand RequestFileCommand { get; set; }
        public RelayCommand SaveFileCommand { get; set; }

        public MainViewModel()
        {
            Messenger.Register<RequestedFile>(this.ReceiveRequestedFile);
            Messenger.Register<RemoteFileInfo>(this.ReceiveRemoteFileInfo);

            this.RequestFileCommand = new RelayCommand(this.RequestFileCommandExecute);
            this.SaveFileCommand = new RelayCommand(this.SaveFileCommandExecute);

            this.RecentFiles = new ObservableCollection<RemoteFileInfo>(CacheHelper.GetRecentFiles());
        }

        private async void RequestRecentFile(RemoteFileInfo value)
        {
            try
            {
                var response = await DataHelper.FetchRemoteFile(this.SelectedFile);
                if (response != null)
                {
                    this.currentHost = this.SelectedFile.RemoteHost;
                    this.currentAuthKey = this.SelectedFile.AuthKey;

                    this.CurrentFileName = response.Data.FileName;
                    this.CurrentFileContent = response.Data.FileContent;
                }
                else
                {
                    MessageBox.Show("Empty response!", "Error");
                }
            }
            catch (TaskCanceledException e)
            {
                MessageBox.Show("Connection timed out!", "Error");
            }
            catch (HttpRequestException e)
            {
                MessageBox.Show("Connection timed out!!", "Error");
            }
        }

        private async void SaveFileCommandExecute()
        {
            var rq = new RequestedFile()
            {
                FileName = this.CurrentFileName,
                FileContent = this.CurrentFileContent
            };
            var json = JsonSerializer.Serialize(rq);

            var response = await HttpHelper.PutRequestAsync("http://" + this.currentHost + "/file", json, this.currentAuthKey);
            if (response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                var respFile = JsonSerializer.Deserialize<FilePostResponse>(body);
                if (respFile.Status == "success")
                {
                    MessageBox.Show("File saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    CacheHelper.SaveRecentFiles(this.RecentFiles);
                    return;
                }
            }

            MessageBox.Show("Error saving file!", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        private void ReceiveRemoteFileInfo(object obj)
        {
            var f = obj as RemoteFileInfo;
            if (f == null)
            {
                MessageBox.Show("Could not fetch file!", "Error");
                return;
            }
                
            this.RecentFiles.Add(f);
            this.currentHost = f.RemoteHost;
            this.currentAuthKey = f.AuthKey;
        }

        private void ReceiveRequestedFile(object obj)
        {
            var file = obj as RequestedFile;
            if (file == null)
                return;
            this.CurrentFileName = file.FileName;
            this.CurrentFileContent = file.FileContent;

        }

        private void RequestFileCommandExecute()
        {
            WindowManager.OpenRequestFileWindow();
        }

        private void LoadRecentFiles()
        {
            this.RecentFiles = new ObservableCollection<RemoteFileInfo>(CacheHelper.GetRecentFiles());
        }
    }
}
