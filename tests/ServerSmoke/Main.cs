using System;
using System.IO;
using System.Xml.Serialization;
using GTA.Math;
using RageCoop.Server.Scripting;

namespace StubSmoke
{
    // Same shape as the Race resource's map class.
    [XmlRoot(ElementName = "Race")]
    public class Map
    {
        [XmlArrayItem(ElementName = "Vector3")]
        public Vector3[] Checkpoints;
        public SpawnPoint[] SpawnPoints;
        public string Name;
    }

    public class SpawnPoint
    {
        public Vector3 Position;
        public float Heading;
    }

    public class Main : ServerScript
    {
        private const string MapXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Race>\n" +
            "\t<Checkpoints>\n" +
            "\t\t<Vector3><X>1167.5</X><Y>-2077.25</Y><Z>41.5</Z></Vector3>\n" +
            "\t</Checkpoints>\n" +
            "\t<SpawnPoints><SpawnPoint><Position><X>1</X><Y>2</Y><Z>3</Z></Position><Heading>90</Heading></SpawnPoint></SpawnPoints>\n" +
            "\t<Name>smoke</Name>\n" +
            "</Race>";

        public override void OnStart()
        {
            var map = (Map)new XmlSerializer(typeof(Map)).Deserialize(new StringReader(MapXml));
            var c = map.Checkpoints[0];
            var s = map.SpawnPoints[0].Position;
            // Written straight to the console: tests/smoke-server.ps1 reads the server's output.
            if (c.X == 1167.5f && c.Z == 41.5f && s.Y == 2f)
                Console.WriteLine("SMOKE-OK map with GTA.Math.Vector3 loaded");
            else
                Console.WriteLine("SMOKE-FAIL map values did not round-trip");
        }

        public override void OnStop() { }
    }
}
