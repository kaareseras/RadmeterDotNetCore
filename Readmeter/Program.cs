using M2Mqtt;
using M2Mqtt.Messages;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Readmeter
{
    class Program
    {

        Timer _tm = null;

        AutoResetEvent _autoEvent = null;

        private int _counter = 0;

        static void Main(string[] args)
        {
            int interval = 10000;
            string mqttIP = "192.168.1.11";
            string meterIP = "192.168.1.10";

            if (Environment.GetEnvironmentVariable("interval") == null) { Environment.SetEnvironmentVariable("interval", "10000"); };
            if (Environment.GetEnvironmentVariable("mqttIP") == null) { Environment.SetEnvironmentVariable("mqttIP", "192.168.1.11"); };
            if (Environment.GetEnvironmentVariable("meterIP") == null) { Environment.SetEnvironmentVariable("meterIP", "192.168.1.10"); };

            try
            {
                interval = Convert.ToInt16(Environment.GetEnvironmentVariable("interval"));
                mqttIP = Environment.GetEnvironmentVariable("mqttIP");
                meterIP = Environment.GetEnvironmentVariable("meterIP");

                Program p = new Program();
                p.StartTimer();
            }
            catch
            {
                Console.WriteLine("Error retrieving enviroment variables");
            }
        }

        public void StartTimer()
        {
            _autoEvent = new AutoResetEvent(false);
            _tm = new Timer(Execute, _autoEvent, 1000, Convert.ToInt16(Environment.GetEnvironmentVariable("interval")));
            Console.Read();
        }

        public void Execute(Object stateInfo)
        {
            ReadMeter(Environment.GetEnvironmentVariable("mqttIP"), Environment.GetEnvironmentVariable("meterIP"));
        }



        private static void ReadMeter(string mqttIP, string meterIP) {

            MqttClient MQTTClient = new MqttClient(IPAddress.Parse(mqttIP));

            string clientId = Guid.NewGuid().ToString();
            MQTTClient.Connect(clientId);

            string context = "";
            string[] arrNames = new string[] { "Energy, Active import", "Power, Active", "Power, Active, L1", "Power, Active, L2", "Power, Active, L3", "Voltage, L1", "Voltage, L2", "Voltage, L3", "Current, L1", "Current, L2", "Current, L3", "Frequency", "Power factor" };
            string[] arrValues = new string[arrNames.Length] ;

            context = new TimedWebClient { Timeout = 10000 }.DownloadString($"http://{meterIP}/static?path=/newsite/meterdata/");

            //Get values for each measure exept Power factor
            for (int i = 0; i < arrNames.Length - 1; i++)
            {
                arrValues[i] = getValueByName(arrNames[i], context, ' ');
            }
            //Get values for Power factor
            arrValues[arrNames.Length - 1] = getValueByName(arrNames[arrNames.Length - 1], context, '<');

            //Print out values
            for (int i = 0; i < arrNames.Length; i++)
            {
                Console.WriteLine(arrNames[i] + ": " + arrValues[i]);
            }

            //Send Values to MQTT broker
            for (int i = 0; i < arrNames.Length; i++)
            {
                MQTTClient.Publish($"meter/{arrNames[i]}", Encoding.UTF8.GetBytes(arrValues[i]), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            }

            Console.WriteLine("Done!!");


            

            string getValueByName(string valueName, string text, char delim)
            {
                string indexText = valueName;
                int indexTextLength = valueName.Length;
                int indexStart = text.IndexOf(indexText) + indexTextLength + 5;
                int indexLength = text.IndexOf(delim, indexStart) - indexStart;
                return text.Substring(indexStart, indexLength);
            }

        }

    }

    public class TimedWebClient : WebClient
    {
        // Timeout in milliseconds, default = 600,000 msec
        public int Timeout { get; set; }

        public TimedWebClient()
        {
            this.Timeout = 600000;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var objWebRequest = base.GetWebRequest(address);
            objWebRequest.Timeout = this.Timeout;
            return objWebRequest;
        }
    }


}
