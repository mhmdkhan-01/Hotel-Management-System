using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Hotel_Management_System.Hubs
{
    public class NotificationHub : Hub
    {
        // Thread-safe dictionary running in server memory: Key = TableNumber, Value = Waiter Handling Status
        private static readonly ConcurrentDictionary<string, string> ActiveWaiterCalls = new();

        // 1. Triggered by Customer phone
        public async Task CallWaiter(string tableNumber)
        {
            // Set default initial state as Awaiting help
            ActiveWaiterCalls[tableNumber] = "Awaiting Waiter";

            // Broadcast live table request alert status down to all connected waiters
            await Clients.All.SendAsync("ReceiveWaiterCall", tableNumber, "Awaiting Waiter");
        }

        // 2. Triggered by Waiter phone to claim responsibility
        public async Task AttendTable(string tableNumber, string waiterName)
        {
            string statusText = $"Attended by {waiterName}";
            ActiveWaiterCalls[tableNumber] = statusText;

            // Broadcast immediate update to all waiters to prevent multiple people walking over
            await Clients.All.SendAsync("ReceiveStatusUpdate", tableNumber, statusText);
        }

        // 3. Triggered by Waiter when leaving the table to clear it out of the queue
        public async Task ClearTableCall(string tableNumber)
        {
            ActiveWaiterCalls.TryRemove(tableNumber, out _);
            await Clients.All.SendAsync("ReceiveCallCleared", tableNumber);
        }

        // Automatically sync newly logged-in waiters with existing active calls
        public override async Task OnConnectedAsync()
        {
            foreach (var call in ActiveWaiterCalls)
            {
                await Clients.Caller.SendAsync("ReceiveWaiterCall", call.Key, call.Value);
            }
            await base.OnConnectedAsync();
        }
        // Inside NotificationHub.cs
        public async Task NotifyAdminOfOrderUpdate()
        {
            // Broadcasts an event to anyone listening for "RefreshAdminDashboard"
            await Clients.All.SendAsync("RefreshAdminDashboard");
        }
        // Inside NotificationHub.cs
        public async Task NotifyFloorOfOrderChanges()
        {
            // Broadcasts to all active screens that a food item changed states
            await Clients.All.SendAsync("RefreshWaiterFloor");
        }
        public async Task NotifyKitchenOfOrder()
        {
            await Clients.All.SendAsync("RefreshKitchenDashboard");
        }
    }
}