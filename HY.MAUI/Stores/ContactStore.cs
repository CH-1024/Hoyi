using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HY.MAUI.Stores
{
    public class ContactStore
    {
        readonly IGlobalCache _globalCache;

        public ContactStore(IGlobalCache globalCache)
        {
            _globalCache = globalCache;
        }

        public ObservableCollection<ContactVM> Contacts { get; } = new();

        public ContactVM? GetContact(long contactId)
        {
            var currentUser = _globalCache.GetCurrentUser();
            if (contactId == currentUser.Id)
            {
                return new ContactVM
                {
                    Contact_Id = contactId,
                    Nickname = currentUser.Nickname,
                    Avatar = currentUser.Avatar,
                    Region = currentUser.Region,
                };
            }

            return Contacts.FirstOrDefault(x => x.Contact_Id == contactId);
        }

        public void Upsert(ContactVM contact)
        {
            var existing = Contacts.FirstOrDefault(c => c.Contact_Id == contact.Contact_Id);
            if (existing != null)
            {
                existing.Contact_Id = contact.Contact_Id;
                existing.Nickname = contact.Nickname;
                existing.Avatar = contact.Avatar;
                existing.Region = contact.Region;
                existing.Remark = contact.Remark;
                existing.Contact_Status = contact.Contact_Status;
                existing.Relation_Request_Status = contact.Relation_Request_Status;
                existing.Relation_Status = contact.Relation_Status;
            }
            else
            {
                Contacts.Add(contact);
            }
        }

        public void ReplaceSnapshot(IEnumerable<ContactVM> contacts)
        {
            var incoming = contacts.ToList();
            var ids = incoming.Select(x => x.Contact_Id).ToHashSet();

            foreach (var stale in Contacts.Where(x => !ids.Contains(x.Contact_Id)).ToList())
                Contacts.Remove(stale);

            for (var i = 0; i < incoming.Count; i++)
            {
                var item = incoming[i];
                var existing = Contacts.FirstOrDefault(x => x.Contact_Id == item.Contact_Id);
                if (existing == null)
                {
                    Contacts.Insert(Math.Min(i, Contacts.Count), item);
                    continue;
                }

                existing.Nickname = item.Nickname;
                existing.Avatar = item.Avatar;
                existing.Region = item.Region;
                existing.Remark = item.Remark;
                existing.Contact_Status = item.Contact_Status;
                existing.Relation_Request_Status = item.Relation_Request_Status;
                existing.Relation_Status = item.Relation_Status;

                var oldIndex = Contacts.IndexOf(existing);
                if (oldIndex != i)
                    Contacts.Move(oldIndex, Math.Min(i, Contacts.Count - 1));
            }
        }

        public bool Remove(long contactId)
        {
            var contact = Contacts.FirstOrDefault(x => x.Contact_Id == contactId);
            return contact != null && Contacts.Remove(contact);
        }

        public void Clear() => Contacts.Clear();
    }
}
