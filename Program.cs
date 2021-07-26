using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using System;
using System.Threading.Tasks;

namespace CreateVirtualMachineSample
{
    public class Program
    {
        private const string dummySSHKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQC+wWK73dCr+jgQOAxNsHAnNNNMEMWOHYEccp6wJm2gotpr9katuF/ZAdou5AaW1C61slRkHRkpRRX9FA9CYBiitZgvCCz+3nWNN7l/Up54Zps/pHWGZLHNJZRYyAB6j5yVLMVHIHriY49d/GZTZVNB8GoJv9Gakwc/fuEZYYl4YDFiGMBP///TzlI4jhiJzjKnEvqPFki5p2ZRJqcbCiF4pJrxUQR/RXqVFQdbRLZgYfJ8xGB878RENq3yQ39d8dVOkq4edbkzwcUmwwwkYVPIoDGsYLaRHnG+To7FvMeyO7xDVQkMKzopTQV8AuKpyvpqu0a9pWOMaiCyDytO7GGN you@me.com";

        private ArmClient _armClient;
        private ResourceGroup _resourceGroup;
        private VirtualNetwork _virtualNetwork;
        private NetworkInterface _networkInterface;
        private VirtualMachine _virtualMachine;

        private Location DefaultLocation => Location.WestUS2;

        public Program()
        {
            _armClient = new ArmClient(new DefaultAzureCredential());
        }

        public static void Main(string[] args)
        {
            var p = new Program();
            try
            {
                p.Create().GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                p.Cleanup().GetAwaiter().GetResult();
            }
        }

        public async Task Create()
        {
            await CreateResourceGroup();
            await CreateVirtualNetwork();
            await CreateNetworkInterface();
            await CreateVirtualMachine();
        }

        private async Task Cleanup()
        {
            await DeleteResourceGroup();
        }

        private async Task CreateResourceGroup()
        {
            _resourceGroup = await _armClient.DefaultSubscription.GetResourceGroups().CreateOrUpdateAsync(
                "testRG",
                new ResourceGroupData(DefaultLocation));
            Console.WriteLine($"Resource Group {_resourceGroup.Id} created");
        }

        private async Task DeleteResourceGroup()
        {
            await _resourceGroup?.DeleteAsync();
        }

        private async Task CreateVirtualNetwork()
        {
            var vnetData = new VirtualNetworkData()
            {
                Location = DefaultLocation,
                AddressSpace = new AddressSpace()
                {
                    AddressPrefixes = { "10.0.0.0/16" }
                },
                Subnets =
                {
                    new SubnetData()
                    {
                        Name = "testSubnet",
                        AddressPrefix = "10.0.2.0/24"
                    }
                }
            };
            _virtualNetwork = await _resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync("testVnet", vnetData);
            Console.WriteLine($"Virtual Network {_virtualNetwork.Id} created");
        }

        private async Task CreateNetworkInterface()
        {
            var networkInterfaceData = new NetworkInterfaceData()
            {
                Location = DefaultLocation,
                IpConfigurations =
                {
                    new NetworkInterfaceIPConfiguration()
                    {
                        Name = "internal",
                        Primary = true,
                        Subnet = new SubnetData()
                        {
                            Id = _virtualNetwork.Data.Subnets[0].Id
                        }
                    }
                }
            };
            _networkInterface = await _resourceGroup.GetNetworkInterfaces().CreateOrUpdateAsync("testNIC", networkInterfaceData);
            Console.WriteLine($"Network Interface {_networkInterface.Id} created");
        }

        private async Task CreateVirtualMachine()
        {
            var virtualMachineData = new VirtualMachineData(DefaultLocation)
            {
                HardwareProfile = new HardwareProfile()
                {
                    VmSize = VirtualMachineSizeTypes.StandardF2
                },
                OsProfile = new OSProfile()
                {
                    AdminUsername = "adminUser",
                    ComputerName = "testVM",
                    LinuxConfiguration = new LinuxConfiguration()
                    {
                        DisablePasswordAuthentication = true,
                        Ssh = new SshConfiguration()
                        {
                            PublicKeys = {
                                new SshPublicKeyInfo()
                                {
                                    Path = $"/home/adminUser/.ssh/authorized_keys",
                                    KeyData = dummySSHKey,
                                }
                            }
                        }
                    }
                },
                NetworkProfile = new Azure.ResourceManager.Compute.Models.NetworkProfile()
                {
                    NetworkInterfaces =
                    {
                        new NetworkInterfaceReference()
                        {
                            Id = _networkInterface.Id,
                            Primary = true,
                        }
                    }
                },
                StorageProfile = new StorageProfile()
                {
                    OsDisk = new OSDisk(DiskCreateOptionTypes.FromImage)
                    {
                        OsType = OperatingSystemTypes.Linux,
                        Caching = CachingTypes.ReadWrite,
                        ManagedDisk = new ManagedDiskParameters()
                        {
                            StorageAccountType = StorageAccountTypes.StandardLRS
                        }
                    },
                    ImageReference = new ImageReference()
                    {
                        Publisher = "Canonical",
                        Offer = "UbuntuServer",
                        Sku = "16.04-LTS",
                        Version = "latest",
                    }
                }
            };
            _virtualMachine = await _resourceGroup.GetVirtualMachines().CreateOrUpdateAsync("testVM", virtualMachineData);
            Console.WriteLine($"Virtual Machine {_virtualMachine.Id} created");
        }
    }
}
