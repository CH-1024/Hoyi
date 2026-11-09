using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Services
{
    public class GlobalBinding : ObservableObject
    {
        private int unreadMessageCount;
        public int UnreadMessageCount
        {
            get { return unreadMessageCount; }
            set
            {
                unreadMessageCount = value;
                OnPropertyChanged(nameof(UnreadMessageCountShow));
                OnPropertyChanged(nameof(UnreadMessageCountVisible));
            }
        }
        public string UnreadMessageCountShow => unreadMessageCount > 99 ? "99+" : $"{unreadMessageCount}";
        public bool UnreadMessageCountVisible => unreadMessageCount > 0;



        private int requestContactCount;
        public int RequestContactCount
        {
            get { return requestContactCount; }
            set
            {
                requestContactCount = value;
                OnPropertyChanged(nameof(RequestContactCountShow));
                OnPropertyChanged(nameof(RequestContactCountVisible));
                OnPropertyChanged(nameof(UnreadContactCountShow));
                OnPropertyChanged(nameof(UnreadContactCountVisible));
            }
        }
        public string RequestContactCountShow => requestContactCount > 99 ? "99+" : $"{requestContactCount}";
        public bool RequestContactCountVisible => requestContactCount > 0;

        private int respondContactCount;
        public int RespondContactCount
        {
            get { return respondContactCount; }
            set
            {
                respondContactCount = value;
                OnPropertyChanged(nameof(RespondContactCountShow));
                OnPropertyChanged(nameof(RespondContactCountVisible));
                OnPropertyChanged(nameof(UnreadContactCountShow));
                OnPropertyChanged(nameof(UnreadContactCountVisible));
            }
        }
        public string RespondContactCountShow => respondContactCount > 99 ? "99+" : $"{respondContactCount}";
        public bool RespondContactCountVisible => respondContactCount > 0;

        public string UnreadContactCountShow => /*requestContactCount +*/ respondContactCount > 99 ? "99+" : $"{/*requestContactCount +*/ respondContactCount}";
        public bool UnreadContactCountVisible => /*requestContactCount +*/ respondContactCount > 0;

    }
}
