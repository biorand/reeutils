// Quick test to validate AimapFile parsing on all sample files

using System;
using System.IO;
using IntelOrca.Biohazard.REE;

class Program
{
    static void Main()
    {
        string[] files = new[]
        {
            @"e:\Projects\Library\reeutils\src\Namsku.BioHazard.REE.RszViewer\location_laboratory_licker_around.aimap.27",
            @"e:\Projects\Library\reeutils\src\Namsku.BioHazard.REE.RszViewer\location_laboratory_tyrant_around.aimap.27",
            @"e:\Projects\Library\reeutils\src\Namsku.BioHazard.REE.RszViewer\location_orphanapproach_dog_around.aimap.27",
            @"e:\Projects\Library\reeutils\src\Namsku.BioHazard.REE.RszViewer\location_orphanasylum_dog_around.aimap.27",
            @"e:\Projects\Library\reeutils\src\Namsku.BioHazard.REE.RszViewer\location_rpd_tyrant_around.aimap.27",
        };

        Console.WriteLine("AIMAP Parser Test");
        Console.WriteLine(new string('=', 80));

        foreach (var file in files)
        {
            Console.WriteLine($"\nFile: {Path.GetFileName(file)}");
            try
            {
                var aimap = new AimapFile(file);
                Console.WriteLine($"  OK: {aimap}");
                Console.WriteLine($"      TypeName: {aimap.TypeName}");
                Console.WriteLine($"      GUID: {aimap.MapGuid}");
                Console.WriteLine($"      RSZ Instances: {aimap.Rsz.InstanceCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAILED: {ex.Message}");
            }
        }
    }
}
