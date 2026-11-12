using CommunityToolkit.Maui.Core.Extensions;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HY.MAUI.Stores
{
    public class ContactRequestStore
    {
        readonly IGlobalCache _globalCache;

        public ContactRequestStore(IGlobalCache globalCache)
        {
            _globalCache = globalCache;
        }

        public ObservableCollection<ContactRequestVM> ContactRequests { get; } = new();

        public void Upsert(ContactRequestVM contactRequestVM)
        {
            var existing = ContactRequests.FirstOrDefault(cr =>
                cr.Sender_Id == contactRequestVM.Sender_Id &&
                cr.Receiver_Id == contactRequestVM.Receiver_Id);

            if (existing != null)
            {
                existing.Id = contactRequestVM.Id;
                existing.Sender_Id = contactRequestVM.Sender_Id;
                existing.Sender_Avatar = contactRequestVM.Sender_Avatar;
                existing.Sender_Nickname = contactRequestVM.Sender_Nickname;
                existing.Receiver_Id = contactRequestVM.Receiver_Id;
                existing.Receiver_Avatar = contactRequestVM.Receiver_Avatar;
                existing.Receiver_Nickname = contactRequestVM.Receiver_Nickname;
                existing.Message = contactRequestVM.Message;
                existing.Source = contactRequestVM.Source;
                existing.Relation_Request_Status = contactRequestVM.Relation_Request_Status;
                existing.Created_At = contactRequestVM.Created_At;
                existing.Handled_At = contactRequestVM.Handled_At;
            }
            else
            {
                ContactRequests.Add(contactRequestVM);
            }
        }

        public void ReplaceSnapshot(IEnumerable<ContactRequestVM> requests)
        {
            var incoming = requests.ToList();
            var keys = incoming.Select(x => (x.Sender_Id, x.Receiver_Id)).ToHashSet();

            foreach (var stale in ContactRequests
                .Where(x => !keys.Contains((x.Sender_Id, x.Receiver_Id))).ToList())
                ContactRequests.Remove(stale);

            foreach (var item in incoming)
                Upsert(item);

            var ordered = incoming
                .Select(x => ContactRequests.First(r =>
                    r.Sender_Id == x.Sender_Id && r.Receiver_Id == x.Receiver_Id))
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                var oldIndex = ContactRequests.IndexOf(ordered[i]);
                if (oldIndex != i)
                    ContactRequests.Move(oldIndex, i);
            }
        }

        public void Clear() => ContactRequests.Clear();

        public int GetRequestCount()
        {
            var currentUser = _globalCache.GetCurrentUser();
            return ContactRequests.Count(cr =>
                cr.Relation_Request_Status == RelationRequestStatus.Pending &&
                cr.Sender_Id == currentUser.Id);
        }

        public int GetRespondCount()
        {
            var currentUser = _globalCache.GetCurrentUser();
            return ContactRequests.Count(cr =>
                cr.Relation_Request_Status == RelationRequestStatus.Pending &&
                cr.Receiver_Id == currentUser.Id);
        }

        public ObservableCollection<ContactRequestVM> GetRequestCollection()
        {
            var currentUser = _globalCache.GetCurrentUser();
            return ContactRequests
                .Where(cr => cr.Sender_Id == currentUser.Id)
                .OrderByDescending(v => v.Created_At)
                .ToObservableCollection();
        }

        public ObservableCollection<ContactRequestVM> GetRespondCollection()
        {
            var currentUser = _globalCache.GetCurrentUser();
            return ContactRequests
                .Where(cr => cr.Receiver_Id == currentUser.Id)
                .ToObservableCollection();
        }
    }
}
