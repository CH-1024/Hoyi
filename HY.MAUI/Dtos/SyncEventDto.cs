using HY.MAUI.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Dtos
{
    public class SyncEventDto
    {
        public long Id { get; set; }
        public SyncEventTypes Event_Type { get; set; }
        public long Entity_Id { get; set; }
        public long Related_Id { get; set; }
        public DateTime Created_At { get; set; }
    }
}
