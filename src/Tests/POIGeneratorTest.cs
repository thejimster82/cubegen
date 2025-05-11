using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Generation.POI;
using CubeGen.World.Common;

namespace CubeGen.Tests
{
    /// <summary>
    /// Test class for the new POI generation system
    /// </summary>
    public partial class POIGeneratorTest : Node3D
    {
        [Export] public int Seed { get; set; } = 12345;
        [Export] public int ChunkSize { get; set; } = 16;
        [Export] public int TestRadius { get; set; } = 5; // Reduced from 10 to make the test run faster

        private Label _resultLabel;
        private Button _runTestButton;

        public override void _Ready()
        {
            // Create UI
            CreateUI();

            // Connect button signal
            _runTestButton.Pressed += RunTest;
        }

        private void CreateUI()
        {
            // Create a control node for UI
            Control uiControl = new Control();
            uiControl.AnchorRight = 1;
            uiControl.AnchorBottom = 1;
            AddChild(uiControl);

            // Create a VBoxContainer for layout
            VBoxContainer vbox = new VBoxContainer();
            vbox.AnchorRight = 1;
            vbox.AnchorBottom = 1;
            vbox.Size = new Vector2(600, 400);
            vbox.Position = new Vector2(20, 20);
            uiControl.AddChild(vbox);

            // Add a title label
            Label titleLabel = new Label();
            titleLabel.Text = "POI Generator Test";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(titleLabel);

            // Add a button to run the test
            _runTestButton = new Button();
            _runTestButton.Text = "Run Test";
            _runTestButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            vbox.AddChild(_runTestButton);

            // Add a label for results
            _resultLabel = new Label();
            _resultLabel.Text = "Press the button to run the test.";
            _resultLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(_resultLabel);
        }

        private void RunTest()
        {
            _resultLabel.Text = "Running test...\n";

            // Initialize the POI generator with the seed
            POIGenerator.Instance.Initialize(Seed);

            // Test 1: Check if POIs are generated consistently
            TestConsistency();

            // Test 2: Check if POIs affecting chunks are found correctly
            TestChunkAffectingPOIs();

            // Test 3: Test performance
            TestPerformance();

            _resultLabel.Text += "\nAll tests completed.";
        }

        private void TestConsistency()
        {
            _resultLabel.Text += "Testing POI consistency...\n";

            // Generate a POI at a specific location
            Vector2I testPos = new Vector2I(100, 100);
            PointOfInterest poi1 = POIGenerator.Instance.GetPOIAt(testPos.X, testPos.Y);

            // Generate it again and check if it's the same
            PointOfInterest poi2 = POIGenerator.Instance.GetPOIAt(testPos.X, testPos.Y);

            if (poi1 == null && poi2 == null)
            {
                _resultLabel.Text += "No POI at test position. Trying another position...\n";

                // Try another position
                testPos = new Vector2I(200, 200);
                poi1 = POIGenerator.Instance.GetPOIAt(testPos.X, testPos.Y);
                poi2 = POIGenerator.Instance.GetPOIAt(testPos.X, testPos.Y);
            }

            if (poi1 != null && poi2 != null)
            {
                bool sameType = poi1.Type == poi2.Type;
                bool sameSize = poi1.Size == poi2.Size;
                bool sameBiome = poi1.Biome == poi2.Biome;

                _resultLabel.Text += $"POI consistency test: " +
                    (sameType && sameSize && sameBiome ? "PASSED" : "FAILED") + "\n";

                if (!(sameType && sameSize && sameBiome))
                {
                    _resultLabel.Text += $"Type: {poi1.Type} vs {poi2.Type}\n";
                    _resultLabel.Text += $"Size: {poi1.Size} vs {poi2.Size}\n";
                    _resultLabel.Text += $"Biome: {poi1.Biome} vs {poi2.Biome}\n";
                }
                else
                {
                    _resultLabel.Text += $"Found consistent POI of type {poi1.Type} at {testPos}\n";
                }
            }
            else
            {
                _resultLabel.Text += "Could not find a POI for consistency test.\n";
            }
        }

        private void TestChunkAffectingPOIs()
        {
            _resultLabel.Text += "Testing chunk-affecting POIs...\n";

            // Test a few chunks
            int poiCount = 0;
            int chunksWithPOIs = 0;
            int totalChunks = 0;

            for (int x = -TestRadius; x <= TestRadius; x++)
            {
                for (int z = -TestRadius; z <= TestRadius; z++)
                {
                    Vector2I chunkPos = new Vector2I(x, z);
                    List<PointOfInterest> pois = POIGenerator.Instance.GetPOIsAffectingChunk(chunkPos, ChunkSize, 80);

                    totalChunks++;
                    if (pois.Count > 0)
                    {
                        chunksWithPOIs++;
                        poiCount += pois.Count;
                    }
                }
            }

            float avgPoisPerChunk = (float)poiCount / totalChunks;
            float percentChunksWithPOIs = (float)chunksWithPOIs / totalChunks * 100;

            _resultLabel.Text += $"Tested {totalChunks} chunks.\n";
            _resultLabel.Text += $"Found {poiCount} POIs affecting chunks.\n";
            _resultLabel.Text += $"{chunksWithPOIs} chunks ({percentChunksWithPOIs:F1}%) have at least one POI.\n";
            _resultLabel.Text += $"Average POIs per chunk: {avgPoisPerChunk:F2}\n";
        }

        private void TestPerformance()
        {
            _resultLabel.Text += "Testing performance...\n";

            // Measure time to generate POIs for many chunks
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

            // Test 1: Time to check if POIs exist at positions
            stopwatch.Start();
            int poiExistCount = 0;
            int totalPositions = 1000;

            for (int i = 0; i < totalPositions; i++)
            {
                int x = i * 10;
                int z = i * 10;
                if (POIGenerator.Instance.DoesPOIExistAt(x, z))
                {
                    poiExistCount++;
                }
            }

            stopwatch.Stop();
            double existCheckTime = stopwatch.ElapsedMilliseconds;

            // Test 2: Time to get POIs affecting chunks
            stopwatch.Restart();
            int totalChunks = 100;
            int totalPOIsFound = 0;

            for (int i = 0; i < totalChunks; i++)
            {
                Vector2I chunkPos = new Vector2I(i, i);
                List<PointOfInterest> pois = POIGenerator.Instance.GetPOIsAffectingChunk(chunkPos, ChunkSize, 80);
                totalPOIsFound += pois.Count;
            }

            stopwatch.Stop();
            double chunkCheckTime = stopwatch.ElapsedMilliseconds;

            _resultLabel.Text += $"Time to check {totalPositions} positions for POIs: {existCheckTime}ms\n";
            _resultLabel.Text += $"Found {poiExistCount} positions with POIs.\n";
            _resultLabel.Text += $"Time to get POIs affecting {totalChunks} chunks: {chunkCheckTime}ms\n";
            _resultLabel.Text += $"Found {totalPOIsFound} total POIs affecting chunks.\n";
            _resultLabel.Text += $"Average time per chunk: {chunkCheckTime / totalChunks:F2}ms\n";
        }
    }
}
