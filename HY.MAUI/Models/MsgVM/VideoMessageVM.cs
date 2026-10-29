using HY.MAUI.Communication;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Models.MsgVM
{
    public class VideoMessageVM : MessageVM
    {
        private string? file_Id;
        public string? File_Id
        {
            get { return file_Id; }
            set
            {
                file_Id = value;
                OnPropertyChanged(nameof(Cover_Video_Url));
                OnPropertyChanged(nameof(Compressed_Video_Url));
                OnPropertyChanged(nameof(Original_Video_Url));
            }
        }

        private double uploadProgress;
        public double UploadProgress
        {
            get { return uploadProgress; }
            set { SetProperty(ref uploadProgress, value); }
        }

        public string? Original_Video_Url => ApiUrl.Get_Origin_Video(File_Id);
        public string? Compressed_Video_Url => ApiUrl.Get_Compress_Video(File_Id);
        public string? Cover_Video_Url => ApiUrl.Get_Cover_Video(File_Id);

    }
}
