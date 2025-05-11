using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.Tests
{
    /// <summary>
    /// Test script for the VoxelStore implementation
    /// </summary>
    public partial class VoxelStoreTest : Node3D
    {
        [Export] public int Seed { get; set; } = 0;
        [Export] public int ChunkSize { get; set; } = 16;
        [Export] public int ChunkHeight { get; set; } = 64;
        [Export] public float VoxelScale { get; set; } = 1.0f;
        
        // Test parameters
        [Export] public Vector3 TestSphereCenter { get; set; } = new Vector3(0, 30, 0);
        [Export] public float TestSphereRadius { get; set; } = 5.0f;
        [Export] public VoxelType TestVoxelType { get; set; } = VoxelType.Stone;
        
        // UI elements
        private Label _statusLabel;
        
        public override void _Ready()
        {
            // Create UI for test status
            _statusLabel = new Label();
            _statusLabel.Text = "VoxelStore Test: Ready";
            _statusLabel.Position = new Vector2(10, 10);
            
            var canvas = new CanvasLayer();
            canvas.AddChild(_statusLabel);
            AddChild(canvas);
            
            // Initialize the WorldDataProvider
            WorldDataProvider.Instance.Initialize(Seed, ChunkSize, ChunkHeight, VoxelScale);
            
            // Initialize the VoxelStore
            VoxelStore.Instance.Initialize(WorldDataProvider.Instance, ChunkSize);
            
            // Run tests
            CallDeferred("RunTests");
        }
        
        private void RunTests()
        {
            _statusLabel.Text = "VoxelStore Test: Running tests...";
            
            // Test 1: Set and get individual voxels
            TestSetGetVoxels();
            
            // Test 2: Set voxels in a region
            TestSetVoxelsInRegion();
            
            // Test 3: Set voxels in a sphere
            TestSetVoxelsInSphere();
            
            // Test 4: Test chunk modification tracking
            TestChunkModificationTracking();
            
            _statusLabel.Text = "VoxelStore Test: All tests completed!";
        }
        
        private void TestSetGetVoxels()
        {
            _statusLabel.Text = "VoxelStore Test: Testing Set/Get Voxels...";
            
            // Set some test voxels
            VoxelStore.Instance.SetVoxelType(0, 30, 0, VoxelType.Stone);
            VoxelStore.Instance.SetVoxelType(1, 30, 0, VoxelType.Dirt);
            VoxelStore.Instance.SetVoxelType(0, 30, 1, VoxelType.Grass);
            
            // Verify the voxels were set correctly
            bool success = true;
            success &= VoxelStore.Instance.GetVoxelType(0, 30, 0) == VoxelType.Stone;
            success &= VoxelStore.Instance.GetVoxelType(1, 30, 0) == VoxelType.Dirt;
            success &= VoxelStore.Instance.GetVoxelType(0, 30, 1) == VoxelType.Grass;
            
            if (success)
            {
                GD.Print("TestSetGetVoxels: PASSED");
            }
            else
            {
                GD.Print("TestSetGetVoxels: FAILED");
            }
        }
        
        private void TestSetVoxelsInRegion()
        {
            _statusLabel.Text = "VoxelStore Test: Testing SetVoxelsInRegion...";
            
            // Set a 3x3x3 region of voxels
            VoxelStore.Instance.SetVoxelsInRegion(5, 30, 5, 3, 3, 3, VoxelType.Wood);
            
            // Verify the voxels were set correctly
            bool success = true;
            for (int x = 5; x < 8; x++)
            {
                for (int y = 30; y < 33; y++)
                {
                    for (int z = 5; z < 8; z++)
                    {
                        success &= VoxelStore.Instance.GetVoxelType(x, y, z) == VoxelType.Wood;
                    }
                }
            }
            
            if (success)
            {
                GD.Print("TestSetVoxelsInRegion: PASSED");
            }
            else
            {
                GD.Print("TestSetVoxelsInRegion: FAILED");
            }
        }
        
        private void TestSetVoxelsInSphere()
        {
            _statusLabel.Text = "VoxelStore Test: Testing SetVoxelsInSphere...";
            
            // Set voxels in a sphere
            VoxelStore.Instance.SetVoxelsInSphere(
                (int)TestSphereCenter.X, 
                (int)TestSphereCenter.Y, 
                (int)TestSphereCenter.Z, 
                TestSphereRadius, 
                TestVoxelType);
            
            // Verify some sample points in the sphere
            bool success = true;
            
            // Center should be the test voxel type
            success &= VoxelStore.Instance.GetVoxelType(
                (int)TestSphereCenter.X, 
                (int)TestSphereCenter.Y, 
                (int)TestSphereCenter.Z) == TestVoxelType;
            
            // Points at the radius should be the test voxel type
            success &= VoxelStore.Instance.GetVoxelType(
                (int)TestSphereCenter.X + (int)TestSphereRadius - 1, 
                (int)TestSphereCenter.Y, 
                (int)TestSphereCenter.Z) == TestVoxelType;
            
            // Points outside the radius should not be the test voxel type
            success &= VoxelStore.Instance.GetVoxelType(
                (int)TestSphereCenter.X + (int)TestSphereRadius + 2, 
                (int)TestSphereCenter.Y, 
                (int)TestSphereCenter.Z) != TestVoxelType;
            
            if (success)
            {
                GD.Print("TestSetVoxelsInSphere: PASSED");
            }
            else
            {
                GD.Print("TestSetVoxelsInSphere: FAILED");
            }
        }
        
        private void TestChunkModificationTracking()
        {
            _statusLabel.Text = "VoxelStore Test: Testing Chunk Modification Tracking...";
            
            // Clear any existing modified chunks
            foreach (var chunk in VoxelStore.Instance.GetModifiedChunks())
            {
                VoxelStore.Instance.ClearChunkModified(chunk);
            }
            
            // Modify a voxel to mark a chunk as modified
            VoxelStore.Instance.SetVoxelType(32, 30, 32, VoxelType.Stone);
            
            // Get the chunk position
            Vector2I chunkPos = new Vector2I(
                Mathf.FloorToInt(32 / (float)ChunkSize),
                Mathf.FloorToInt(32 / (float)ChunkSize)
            );
            
            // Verify the chunk was marked as modified
            bool success = VoxelStore.Instance.IsChunkModified(chunkPos);
            
            // Clear the modified flag
            VoxelStore.Instance.ClearChunkModified(chunkPos);
            
            // Verify the chunk is no longer marked as modified
            success &= !VoxelStore.Instance.IsChunkModified(chunkPos);
            
            if (success)
            {
                GD.Print("TestChunkModificationTracking: PASSED");
            }
            else
            {
                GD.Print("TestChunkModificationTracking: FAILED");
            }
        }
    }
}
