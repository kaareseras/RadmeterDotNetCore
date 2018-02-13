using M2Mqtt;
using M2Mqtt.Messages;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Readmeter
{
    class Program
    {
        static void Main(string[] args)
        {

            while (true)
            {
                Console.WriteLine("Fething data from meter");
                
                ReadMeter();
                System.Threading.Thread.Sleep(10000);
            }
        }

        private static void ReadMeter() { 
            MqttClient MQTTClient = new MqttClient(IPAddress.Parse("192.168.1.11"));
            string clientId = Guid.NewGuid().ToString();
            MQTTClient.Connect(clientId);

            string context = "";
            string[] arrNames = new string[] { "Energy, Active import", "Power, Active", "Power, Active, L1", "Power, Active, L2", "Power, Active, L3", "Voltage, L1", "Voltage, L2", "Voltage, L3", "Current, L1", "Current, L2", "Current, L3", "Frequency", "Power factor" };
            string[] arrValues = new string[arrNames.Length] ;

            using (var webClient = new WebClient())
            {

                //Get Webcontent from metter
                context = webClient.DownloadString("http://192.168.1.10/static?path=/newsite/meterdata/");
            }
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
}
