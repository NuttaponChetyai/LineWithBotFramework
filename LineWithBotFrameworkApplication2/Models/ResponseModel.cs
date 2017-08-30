using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LineWithBotFrameworkApplication2.Models
{
    public class ResponseModel
    {
        public string messege { get; set; }
        public bool status { get; set; }
        public object data { get; set; }
        public decimal? amount { get; set; }
        public int total { get; set; }
    }
    public class Invoice {
        public string InvoiceNo { get; set; }
        public string Comment { get; set; }
        public decimal? Amount { get; set; } 
    }
}