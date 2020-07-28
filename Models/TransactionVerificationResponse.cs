using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.PaystackStandard.Models
{
    public class TransactionVerificationResponse
    {
        public bool IsVerified { get; set; }

        public Decimal Amount { get; set; }

        public string AuthCode { get; set; }

        public string Message { get; internal set; }
    }
}
