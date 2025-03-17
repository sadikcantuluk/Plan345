using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using PlanYonetimAraclari.Models;

namespace PlanYonetimAraclari.Hubs
{
    public class TaskHub : Hub
    {
        private static readonly Dictionary<string, List<string>> _projectGroups = new Dictionary<string, List<string>>();
        
        /// <summary>
        /// Kullanıcıyı belirli bir proje grubuna ekler
        /// </summary>
        public async Task JoinProjectGroup(int projectId)
        {
            string groupName = $"project_{projectId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            
            // Grup listesinde yoksa yeni bir liste oluştur
            if (!_projectGroups.ContainsKey(groupName))
            {
                _projectGroups[groupName] = new List<string>();
            }
            
            // Bağlantı ID'sini listeye ekle
            _projectGroups[groupName].Add(Context.ConnectionId);
            
            // Konsola bilgi yaz
            Console.WriteLine($"Kullanıcı {Context.ConnectionId}, {groupName} grubuna katıldı. Gruptaki kullanıcı sayısı: {_projectGroups[groupName].Count}");
        }
        
        /// <summary>
        /// Kullanıcıyı belirli bir proje grubundan çıkarır
        /// </summary>
        public async Task LeaveProjectGroup(int projectId)
        {
            string groupName = $"project_{projectId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            
            // Grup listesinde varsa bağlantı ID'sini kaldır
            if (_projectGroups.ContainsKey(groupName))
            {
                _projectGroups[groupName].Remove(Context.ConnectionId);
                
                // Grupta kimse kalmadıysa grubu kaldır
                if (_projectGroups[groupName].Count == 0)
                {
                    _projectGroups.Remove(groupName);
                }
            }
            
            // Konsola bilgi yaz
            Console.WriteLine($"Kullanıcı {Context.ConnectionId}, {groupName} grubundan ayrıldı.");
        }
        
        /// <summary>
        /// Hub bağlantısı kesildiğinde çağrılır
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // Kullanıcının yer aldığı tüm grupları bul
            List<string> groupsToRemove = new List<string>();
            
            foreach (var group in _projectGroups)
            {
                if (group.Value.Contains(Context.ConnectionId))
                {
                    group.Value.Remove(Context.ConnectionId);
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, group.Key);
                    
                    // Grupta kimse kalmadıysa grubu listeden kaldırmak için işaretle
                    if (group.Value.Count == 0)
                    {
                        groupsToRemove.Add(group.Key);
                    }
                }
            }
            
            // Boş grupları kaldır
            foreach (var group in groupsToRemove)
            {
                _projectGroups.Remove(group);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        public async Task TaskCreated(int projectId, TaskModel task)
        {
            await Clients.Group($"project_{projectId}").SendAsync("ReceiveNewTask", task);
        }

        public async Task TaskStatusChanged(int projectId, int taskId, PlanYonetimAraclari.Models.TaskStatus newStatus)
        {
            await Clients.Group($"project_{projectId}").SendAsync("ReceiveTaskStatusChange", taskId, newStatus);
        }

        public async Task TaskDeleted(int projectId, int taskId)
        {
            await Clients.Group($"project_{projectId}").SendAsync("ReceiveTaskDelete", taskId);
        }
    }
} 