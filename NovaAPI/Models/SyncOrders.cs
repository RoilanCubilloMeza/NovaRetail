using System.Collections.Generic;

namespace NovaAPI.Models
{
    public class SyncOrders
    {
        public List<OrderEntry> orderEntry { get; set; }
        public List<Order> order { get; set; }
        public int StoreID { get; set; }


    }
}