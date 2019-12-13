using System;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using Halibut.ServiceModel;

namespace Halibut.OctopusSample
{
    public partial class TentacleForm : Form
    {
        readonly HalibutRuntime tentacleHalibutRuntime;
        readonly Logger log;

        public TentacleForm()
        {
            InitializeComponent();

            log = new Logger(textBox1);

            var tentacleServices = new DelegateServiceFactory();
            tentacleServices.Register<IHealthCheckService>(() => new HealthCheck(log));

            tentacleHalibutRuntime = new HalibutRuntime(tentacleServices, new X509Certificate2("OctopusTentacle.pfx"));
        }

        public void PollingMode(Uri subscription, ServiceEndPoint octopusServer)
        {
            tentacleHalibutRuntime.Poll(subscription, octopusServer);
            Text = "Tentacle: polling, subscription: " + subscription;
        }

        public void ListeningMode(int port, string octopusThumbprint)
        {
            tentacleHalibutRuntime.Listen(port);
            tentacleHalibutRuntime.Trust(octopusThumbprint);
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
