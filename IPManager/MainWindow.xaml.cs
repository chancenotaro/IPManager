// File: MainWindow.xaml.cs
using IPManager.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;

namespace IPManager
{

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string JobsFilePath = "jobs.json";

        private ObservableCollection<Job> _jobs;
        public ObservableCollection<Job> Jobs
        {
            get { return _jobs; }
            set { _jobs = value; OnPropertyChanged("Jobs"); }
        }

        private Job _currentJob;
        public Job CurrentJob
        {
            get { return _currentJob; }
            set { _currentJob = value; OnPropertyChanged("CurrentJob"); }
        }

        // Collection to hold all network interfaces
        public ObservableCollection<NetworkInterface> NetworkInterfaces { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadJobs();
            cmbJobs.ItemsSource = Jobs;
            LoadNetworkInterfaces();
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion

        #region Job Management

        private void btnAddJob_Click(object sender, RoutedEventArgs e)
        {
            string newJobName = cmbJobs.Text.Trim();
            if (string.IsNullOrEmpty(newJobName))
            {
                MessageBox.Show("Job name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Jobs.Any(j => j.Name.Equals(newJobName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Job already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Job newJob = new Job { Name = newJobName };
            Jobs.Add(newJob);
            SaveJobs();
            cmbJobs.SelectedItem = newJob;
            MessageBox.Show($"Job '{newJobName}' added successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void cmbJobs_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                btnAddJob_Click(sender, e);
                e.Handled = true;
            }
        }

        private void cmbJobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbJobs.SelectedItem is Job selectedJob)
            {
                CurrentJob = selectedJob;
                // Update the DHCP radio buttons based on the job's configuration
                if (selectedJob.UseDHCP)
                {
                    rbDHCP.IsChecked = true;
                }
                else
                {
                    rbStatic.IsChecked = true;
                }

                // Optionally, select the first available network interface by default
                if (cmbInterfaces.Items.Count > 0)
                {
                    cmbInterfaces.SelectedIndex = 0;
                }
            }
            else
            {
                CurrentJob = null;
            }
        }

        #endregion

        #region Network Interface Management

        private void LoadNetworkInterfaces()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                                .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                            n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) //||
                                           // n.NetworkInterfaceType == NetworkInterfaceType.Other) // Include other types as needed
                                .ToList();

            NetworkInterfaces = new ObservableCollection<NetworkInterface>(interfaces);
            cmbInterfaces.ItemsSource = NetworkInterfaces;
            if (cmbInterfaces.Items.Count > 0)
            {
                cmbInterfaces.SelectedIndex = 0;
            }
        }

        private void cmbInterfaces_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Optional: Handle any actions when network interface selection changes
        }

        #endregion

        #region DHCP Selection

        private void rbDHCP_Checked(object sender, RoutedEventArgs e)
        {
            if (CurrentJob != null)
            {
                CurrentJob.UseDHCP = true;
                SaveJobs();
            }
            ToggleIPControls(false);
        }

        private void rbDHCP_Unchecked(object sender, RoutedEventArgs e)
        {
            // No action needed when DHCP is unchecked
        }

        private void rbStatic_Checked(object sender, RoutedEventArgs e)
        {
            if (CurrentJob != null)
            {
                CurrentJob.UseDHCP = false;
                SaveJobs();
            }
            ToggleIPControls(true);
        }

        private void rbStatic_Unchecked(object sender, RoutedEventArgs e)
        {
            // No action needed when Static is unchecked
        }

        private void ToggleIPControls(bool isEnabled)
        {
            lvIPAddresses.IsEnabled = isEnabled;
            txtNewIP.IsEnabled = isEnabled;
            btnAddIP.IsEnabled = isEnabled;

            // Optionally, disable checkboxes and delete buttons when not using static IPs
            if (!isEnabled)
            {
                foreach (var item in lvIPAddresses.Items)
                {
                    if (item is IPAddressEntry ipEntry)
                    {
                        ipEntry.IsSelected = false; // Deselect all when switching to DHCP
                    }
                }
            }
        }

        #endregion

        #region IP Address Management

        private void btnAddIP_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentJob == null)
            {
                MessageBox.Show("Please select or add a job first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (CurrentJob.UseDHCP)
            {
                MessageBox.Show("Cannot add IP addresses when using DHCP.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string newIP = txtNewIP.Text.Trim();
            if (string.IsNullOrEmpty(newIP))
            {
                MessageBox.Show("IP address cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!IsValidIPAddress(newIP))
            {
                MessageBox.Show("Invalid IP address format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (CurrentJob.IPAddresses.Any(ip => ip.Address.Equals(newIP, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("IP address already exists in this job.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CurrentJob.IPAddresses.Add(new IPAddressEntry { Address = newIP, IsSelected = false });
            SaveJobs();
            txtNewIP.Clear();
        }

        private void btnDeleteIP_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is IPAddressEntry ipEntry)
            {
                if (MessageBox.Show($"Are you sure you want to delete IP address {ipEntry.Address}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    CurrentJob.IPAddresses.Remove(ipEntry);
                    SaveJobs();
                }
            }
        }

        private bool IsValidIPAddress(string ip)
        {
            return System.Net.IPAddress.TryParse(ip, out _);
        }

        #endregion

        #region Submit and Apply IP Configuration

        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentJob == null)
            {
                MessageBox.Show("Please select a job.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (cmbInterfaces.SelectedItem is NetworkInterface selectedInterface)
            {
                string interfaceName = selectedInterface.Name;

                if (CurrentJob.UseDHCP)
                {
                    // Apply DHCP
                    ApplyDHCP(interfaceName);
                }
                else
                {
                    var selectedIPs = CurrentJob.IPAddresses.Where(ip => ip.IsSelected).Select(ip => ip.Address).ToList();

                    if (selectedIPs.Count == 0)
                    {
                        MessageBox.Show("Please select at least one IP address.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Apply the selected static IP addresses
                    ApplyStaticIPAddresses(interfaceName, selectedIPs);
                }
            }
            else
            {
                MessageBox.Show("Please select a network interface.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyDHCP(string interfaceName)
        {
            // Set to DHCP
            SetDHCP(interfaceName);

            MessageBox.Show("DHCP configuration applied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyStaticIPAddresses(string interfaceName, List<string> ipAddresses)
        {
            // Clear existing IP configurations by setting DHCP
            SetDHCP(interfaceName);

            // Apply each selected static IP address
            foreach (var ip in ipAddresses)
            {
                SetStaticIPAddress(interfaceName, ip, "255.255.255.0", "192.168.1.1"); // Adjust subnet mask and gateway as needed
            }

            MessageBox.Show("Static IP configuration applied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetStaticIPAddress(string interfaceName, string ipAddress, string subnetMask, string gateway)
        {
            string commandAdd = $"interface ipv4 add address \"{interfaceName}\" {ipAddress} {subnetMask}";
            ExecuteCommand(commandAdd);

            // Optionally, set gateway if it's the first IP
            if (!string.IsNullOrEmpty(gateway))
            {
                string commandGateway = $"interface ipv4 set address \"{interfaceName}\" gateway={gateway} gwmetric=1";
                ExecuteCommand(commandGateway);
            }
        }

        private void SetDHCP(string interfaceName)
        {
            string command = $"interface ip set address \"{interfaceName}\" dhcp";
            ExecuteCommand(command);
        }

        private void ExecuteCommand(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("netsh", arguments)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas" // Runs the process with administrator privileges
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit();
                    string output = process.StandardOutput.ReadToEnd();
                    // Optionally, handle output or errors
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing command: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Data Persistence

        private void SaveJobs()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Jobs, Formatting.Indented);
                File.WriteAllText(JobsFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving jobs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadJobs()
        {
            try
            {
                if (File.Exists(JobsFilePath))
                {
                    string json = File.ReadAllText(JobsFilePath);
                    Jobs = JsonConvert.DeserializeObject<ObservableCollection<Job>>(json) ?? new ObservableCollection<Job>();
                }
                else
                {
                    Jobs = new ObservableCollection<Job>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading jobs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Jobs = new ObservableCollection<Job>();
            }
        }

        #endregion
    }
}
