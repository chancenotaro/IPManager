// File: Models/Job.cs
using System.Collections.ObjectModel;

namespace IPManager.Models
{
    public class Job
    {
        public string Name { get; set; }
        public bool UseDHCP { get; set; } // Determines if the job uses DHCP
        public ObservableCollection<IPAddressEntry> IPAddresses { get; set; }

        public Job()
        {
            IPAddresses = new ObservableCollection<IPAddressEntry>();
            UseDHCP = false; // Default to Static IPs
        }
    }
}
