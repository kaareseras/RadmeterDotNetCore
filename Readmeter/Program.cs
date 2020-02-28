using M2Mqtt;
using M2Mqtt.Messages;
using System;
using System.Collections.Generic;
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

        public class MeterReading {
            public DateTime TS { get; set; }
            public List<Reading> Readings { get; set; }

            public MeterReading (){
                TS = DateTime.Now;
            }
        }

        public class Reading 
	    {
            public Reading (string Name, string ShortName, string Unit, float Value)
            {
                this.Name = Name;
                this.ShortName = ShortName;
                this.Unit = Unit;
                this.Value = Value;
            }
            
            public string Name { get; set; }
            public string ShortName { get; set; }
            public string Unit { get; set; }
            public float Value { get; set; }
	    }

        static void Main(string[] args)
        {
            int interval = 60000;
            string mqttIP = "192.168.1.11";
            string meterIP = "192.168.1.10";

            if (Environment.GetEnvironmentVariable("interval") == null) { Environment.SetEnvironmentVariable("interval", "60000"); };
            if (Environment.GetEnvironmentVariable("mqttIP") == null) { Environment.SetEnvironmentVariable("mqttIP", "192.168.1.11"); };
            if (Environment.GetEnvironmentVariable("meterIP") == null) { Environment.SetEnvironmentVariable("meterIP", "192.168.1.10"); };

            try
            {
                interval = Convert.ToInt32(Environment.GetEnvironmentVariable("interval"));
                mqttIP = Environment.GetEnvironmentVariable("mqttIP");
                meterIP = Environment.GetEnvironmentVariable("meterIP");

                Program p = new Program();
                p.StartTimer();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error retrieving enviroment variables: '{e}'");
            }
        }

        public void StartTimer()
        {
            _autoEvent = new AutoResetEvent(false);
            _tm = new Timer(Execute, _autoEvent, 1000, Convert.ToInt32(Environment.GetEnvironmentVariable("interval")));
            Console.Read();
        }

        public void Execute(Object stateInfo)
        {
            ReadMeter(Environment.GetEnvironmentVariable("mqttIP"), Environment.GetEnvironmentVariable("meterIP"));
        }



        private static void ReadMeter(string mqttIP, string meterIP) {

            MeterReading meterReading = new MeterReading();
            List<Reading> readings = new List<Reading>();

            MqttClient MQTTClient = new MqttClient(IPAddress.Parse(mqttIP));

            string clientId = Guid.NewGuid().ToString();
            MQTTClient.Connect(clientId);

            string context = "";
            string[] arrNames = new string[] { "Energy, Active import", "Power, Active", "Power, Active, L1", "Power, Active, L2", "Power, Active, L3", "Voltage, L1", "Voltage, L2", "Voltage, L3", "Current, L1", "Current, L2", "Current, L3", "Frequency", "Power factor" };
            string[] arrShartNames = new string[] { "E", "P", "P1", "P2", "P3", "U1", "U2", "U3", "I1", "I2", "I3", "f", "cfi" };
            string[] arrUnits = new string[] { "kWh", "W", "W", "W", "W", "V", "V", "V", "A", "A", "A", "Hz", ""  };

            string[] arrValues = new string[arrNames.Length] ;

            try
            {
                context = new TimedWebClient { Timeout = 10000 }.DownloadString($"http://{meterIP}/static?path=/newsite/meterdata/");
            }
            catch
            {
                Console.WriteLine("Error reading meter, skipping");
                return;
            }

            //Get values for each measure exept Power factor
            for (int i = 0; i < arrNames.Length - 1; i++)
            {
                arrValues[i] = getValueByName(arrNames[i], context, ' ');

            }
            //Get values for Power factor
            arrValues[arrNames.Length - 1] = getValueByName(arrNames[arrNames.Length - 1], context, '<');
            
            //Write values into object
            for (int i = 0; i < arrNames.Length ; i++)
            {
                Reading reading = new Reading(arrNames[i], arrShartNames[i], arrUnits[i], float.Parse(arrValues[i], CultureInfo.InvariantCulture.NumberFormat));
                readings.Add(reading);
            }


            meterReading.Readings = readings;

            string strJson = Newtonsoft.Json.JsonConvert.SerializeObject(meterReading);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(strJson);

            //Print out values
            //Console.WriteLine(strJson);
            //for (int i = 0; i < arrNames.Length; i++)
            //{
            //    Console.WriteLine(arrNames[i] + ": " + arrValues[i]);
            //}

            //Send Values to MQTT broker
            MQTTClient.Publish($"meter/reading", bytes, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            //for (int i = 0; i < arrNames.Length; i++)
            //{
            //    MQTTClient.Publish($"meter/{arrNames[i]}", Encoding.UTF8.GetBytes(arrValues[i]), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            //}

            Console.Write("Done!! ");

            Console.WriteLine(DateTime.Now.ToString());


            

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
