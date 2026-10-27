using HY.ApiService.Enums;
using SqlSugar;
using StorageType = HY.ApiService.Enums.StorageType;

namespace HY.ApiService.Entities
{
    [SugarTable("media_storage")]
    public class MediaStorageEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }
        public string File_SHA256 { get; set; } = null!;
        public string File_Path { get; set; } = null!;
        public long File_Size { get; set; }
        public string Mime_Type { get; set; }
        public string Storage_Bucket { get; set; }      // 存储桶
        public StorageType Storage_Type { get; set; }   // 1本地 2OSS 3COS 4S3
        public int Ref_Count { get; set; }
        public int Variant_Mask { get; set; }           // 衍生标记，二进制位表示是否存在对应变体（1: 缩略图, 2: 压缩图, 3: 视频封面, 4: 低清视频）
        public int Status { get; set; }                 // 1正常 0删除
        public DateTime Created_At { get; set; }
    }
}
