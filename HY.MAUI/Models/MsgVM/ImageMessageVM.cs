using HY.MAUI.Communication;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Models.MsgVM
{
    public class ImageMessageVM : MessageVM
    {
        private string? file_Id;
        public string? File_Id
        {
            get { return file_Id; }
            set
            {
                file_Id = value;
                OnPropertyChanged(nameof(Original_Image_Url));
                OnPropertyChanged(nameof(Compressed_Image_Url));
                OnPropertyChanged(nameof(Thumbnail_Image_Url));
            }
        }

        private double uploadProgress;
        public double UploadProgress
        {
            get { return uploadProgress; }
            set { SetProperty(ref uploadProgress, value); }
        }

        public string? Original_Image_Url => ApiUrl.Get_Origin_Image(File_Id);
        public string? Compressed_Image_Url => ApiUrl.Get_Compress_Image(File_Id);
        public string? Thumbnail_Image_Url => ApiUrl.Get_Thumb_Image(File_Id);

    }
}
