using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Liquid.Editor
{
    [InitializeOnLoad]
    public static class Test1FbxBatchEntry
    {
        private static readonly string CommandFilePath =
            Path.Combine(Directory.GetCurrentDirectory(), "Library", "CodexEditorCommand.txt");
        private static readonly string ResultFilePath =
            Path.Combine(Directory.GetCurrentDirectory(), "Library", "CodexEditorCommand.result.txt");
        private static bool commandQueued;

        static Test1FbxBatchEntry()
        {
            string command = System.Environment.GetEnvironmentVariable("LIQUID_BATCH_COMMAND");
            if (!string.IsNullOrWhiteSpace(command))
            {
                QueueCommand(command, true);
                return;
            }

            EditorApplication.update -= PollCommandFile;
            EditorApplication.update += PollCommandFile;
        }

        private static void PollCommandFile()
        {
            if (commandQueued || !File.Exists(CommandFilePath))
            {
                return;
            }

            string command = File.ReadAllText(CommandFilePath).Trim();
            File.Delete(CommandFilePath);
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            QueueCommand(command, false);
        }

        private static void QueueCommand(string command, bool exitWhenDone)
        {
            commandQueued = true;
            EditorApplication.delayCall += () =>
            {
                int exitCode = 0;
                string resultMessage = "OK";
                try
                {
                    switch (command)
                    {
                    case "inspect-test1":
                        Test1FbxSceneTools.InspectTest1Fbx();
                        break;
                    case "replace-test1":
                        Test1FbxSceneTools.ReplaceSquareContainerWithTest1();
                        break;
                    case "replace-text1":
                        Test1FbxSceneTools.ReplaceText1ContainerWithTest1();
                        break;
                    case "inspect-text1-collider":
                        Test1FbxSceneTools.InspectText1ColliderAsset();
                        break;
                    case "inspect-loaded-text1":
                        resultMessage = Test1FbxSceneTools.InspectLoadedText1CollisionState();
                        break;
                    default:
                        UnityEngine.Debug.LogWarning("Unknown LIQUID_BATCH_COMMAND: " + command);
                        resultMessage = "Unknown command: " + command;
                        break;
                    }
                }
                catch (System.Exception exception)
                {
                    exitCode = 1;
                    resultMessage = exception.ToString();
                    UnityEngine.Debug.LogException(exception);
                }
                finally
                {
                    commandQueued = false;
                    try
                    {
                        File.WriteAllText(ResultFilePath, resultMessage);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }

                    if (exitWhenDone)
                    {
                        EditorApplication.Exit(exitCode);
                    }
                }
            };
        }
    }
}
