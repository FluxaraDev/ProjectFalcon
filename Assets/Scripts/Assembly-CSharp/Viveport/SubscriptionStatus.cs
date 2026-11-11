using System.Collections.Generic;

namespace Viveport
{
	public class SubscriptionStatus
	{
		public enum Platform
		{
			Windows = 0,
			Android = 1
		}

		public enum TransactionType
		{
			Unknown = 0,
			Paid = 1,
			Redeem = 2,
			FreeTrial = 3
		}

		public List<Platform> Platforms { get; set; }

		public TransactionType Type { get; set; }

		public SubscriptionStatus()
		{
			Platforms = new List<Platform>();
			Type = TransactionType.Unknown;
		}
	}
}
