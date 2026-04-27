using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NovaAPI.Models
{
    public class NovaRetailManagerDashboardResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("businessDate")]
        public DateTime BusinessDate { get; set; }

        [JsonProperty("salesTodayCount")]
        public int SalesTodayCount { get; set; }

        [JsonProperty("salesTodayTotal")]
        public decimal SalesTodayTotal { get; set; }

        [JsonProperty("quotesCreatedToday")]
        public int QuotesCreatedToday { get; set; }

        [JsonProperty("quotesConvertedToday")]
        public int QuotesConvertedToday { get; set; }

        [JsonProperty("pendingWorkOrders")]
        public int PendingWorkOrders { get; set; }

        [JsonProperty("paymentsReceivedTodayCount")]
        public int PaymentsReceivedTodayCount { get; set; }

        [JsonProperty("paymentsReceivedTodayTotal")]
        public decimal PaymentsReceivedTodayTotal { get; set; }
    }

    public class NovaRetailActionLogResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("actions")]
        public List<NovaRetailActionLogEntryDto> Actions { get; set; } = new List<NovaRetailActionLogEntryDto>();
    }

    public class NovaRetailActionLogEntryDto
    {
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("actionDate")]
        public DateTime ActionDate { get; set; }

        [JsonProperty("actionType")]
        public string ActionType { get; set; } = string.Empty;

        [JsonProperty("entityType")]
        public string EntityType { get; set; } = string.Empty;

        [JsonProperty("entityID")]
        public int EntityID { get; set; }

        [JsonProperty("cashierID")]
        public int CashierID { get; set; }

        [JsonProperty("cashierName")]
        public string CashierName { get; set; } = string.Empty;

        [JsonProperty("storeID")]
        public int StoreID { get; set; }

        [JsonProperty("registerID")]
        public int RegisterID { get; set; }

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("detail")]
        public string Detail { get; set; } = string.Empty;
    }
}
