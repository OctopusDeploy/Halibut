using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using Halibut.ServiceModel;

namespace Halibut.OctopusSample
{
    public partial class OctopusForm : Form
    {
        HalibutRuntime serverHalibutRuntime;
        int listenPort = 10900;
        List<ServiceEndPoint> tentacles = new List<ServiceEndPoint>();
        Logger log;

        public OctopusForm()
        {
            InitializeComponent();

            log = new Logger(logBox);
            var services = new DelegateServiceFactory();
            serverHalibutRuntime = new HalibutRuntime(services, new X509Certificate2("OctopusServer.pfx"));
        }

        private void AddListeningTentacleClicked(object sender, EventArgs e)
        {
            listenPort++;

            var tentacleForm = new TentacleForm();
            tentacleForm.ListeningMode(listenPort, "EF3A7A69AFE0D13130370B44A228F5CD15C069BC");
            tentacleForm.Show();
            tentacles.Add(new ServiceEndPoint(new Uri("https://localhost:" + listenPort), "2074529C99D93D5955FEECA859AEAC6092741205"));
        }

        private void DeployPackageButtonClicked(object sender, EventArgs e)
        {
            Execute.Background(delegate
            {
                foreach (var tentacle in tentacles)
                {
                    log.WriteLine("Checking health of: " + tentacle);
                    var healthCheckService = serverHalibutRuntime.CreateClient<IHealthCheckService>(tentacle);
                    var isOnline = healthCheckService.IsOnline();
                    log.WriteLine("Result: " + isOnline);
                }
            });
        }
    }
}
