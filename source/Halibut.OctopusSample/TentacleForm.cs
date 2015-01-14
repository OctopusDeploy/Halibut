using System;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using Halibut.Client;
using Halibut.Server;
using Halibut.Server.Dispatch;

namespace Halibut.OctopusSample
{
    public partial class TentacleForm : Form
    {
        readonly HalibutRuntime tentacleRuntime;
        readonly Logger log;

        public TentacleForm()
        {
            InitializeComponent();

            log = new Logger(textBox1);

            var tentacleServices = new DelegateServiceFactory();
            tentacleServices.Register<IHealthCheckService>(() => new HealthCheck(log));

            tentacleRuntime = new HalibutRuntime(tentacleServices, new X509Certificate2("OctopusTentacle.pfx"));
        }

        public void PollingMode(Uri subscription, ServiceEndPoint octopusServer)
        {
            tentacleRuntime.Poll(subscription, octopusServer);
            Text = "Tentacle: polling, subscription: " + subscription;
        }

        public void ListeningMode(int port, string octopusThumbprint)
        {
            tentacleRuntime.Listen(port);
            tentacleRuntime.Trust(octopusThumbprint);
            Text = "Tentacle: listening, port: " + port;
        }

        class HealthCheck : IHealthCheckService
        {
            readonly Logger logger;

            public HealthCheck(Logger logger)
            {
                this.logger = logger;
            }

            public bool IsOnline()
            {
                logger.WriteLine("Health check received");
                return true;
            }
        }
    }
}
