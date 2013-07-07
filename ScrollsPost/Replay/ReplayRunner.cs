using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using JsonFx.Json;
using ScrollsModLoader.Interfaces;
using UnityEngine;

namespace ScrollsPost {
    public class ReplayRunner : IBattleModeUICallback, IOkStringCancelCallback, IOkCancelCallback {
        //private ScrollsPost.Mod mod;
        private String replayPrimaryPath;
        private String replaySecondaryPath;
        private String primaryType;
        private Boolean msgPending = false;
        private Thread playerThread;
        private Dictionary<String, object> metadata;

        private Boolean paused = false;
        private Boolean wasPaused = false;
        private float speed = 1;

        private GUIStyle buttonStyle;
        private GUIStyle speedButtonStyle;

        private MethodInfo deselectMethod;
        private FieldInfo reverseField;
        private FieldInfo percentField;

        public ReplayRunner(ScrollsPost.Mod mod, String path) {
            //this.mod = mod;
            this.replayPrimaryPath = path;
            this.primaryType = path.Contains("-white.spr") ? "white" : "black";

            // Check if we have both perspectives available to us
            String secondary = path.Contains("-white.spr") ? path.Replace("-white.spr", "-black.spr") : path.Replace("-black.spr", "-white.spr");
            if( File.Exists(secondary) ) {
                this.replaySecondaryPath = secondary;
            }

            GUISkin skin = (GUISkin)Resources.Load("_GUISkins/LobbyMenu");
            //this.highlightedButtonStyle = new GUIStyle(this.regularUISkin.button);
            //this.highlightedButtonStyle.normal.background = this.highlightedButtonStyle.active.background;
            this.buttonStyle = skin.button;
            this.buttonStyle.normal.background = this.buttonStyle.hover.background;
            this.buttonStyle.normal.textColor = new Color(1f, 1f, 1f, 1f);

            this.buttonStyle.hover.textColor = new Color(0.80f, 0.80f, 0.80f, 1f);

            this.buttonStyle.active.background = this.buttonStyle.hover.background;
            this.buttonStyle.active.textColor = new Color(0.60f, 0.60f, 0.60f, 1f);

            this.speedButtonStyle = new GUIStyle(this.buttonStyle);
            this.speedButtonStyle.fontSize = (int)Math.Round(this.buttonStyle.fontSize * 0.95f);

            playerThread = new Thread(new ThreadStart(Start));
            playerThread.Start();
        }

        public Boolean OnHandleNextMessage() {
            if( msgPending ) {
                msgPending = false;
                return true;
            } else {
                return false;
            }
        }

        public void OnBattleGUI(InvocationInfo info) {
            // Bugs out visually otherwise
            deselectMethod.Invoke(info.target, null);

            // For managing the replay
            int depth = GUI.depth;

            // Container
            Color color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 1f);
            //Rect container = new Rect((float)Screen.width * 0.08f, (float)Screen.height * 0.82f, (float)(Screen.width * 0.08f), (float)Screen.height * 0.16f);
            Rect container = new Rect((float)Screen.width * 0.08f, (float)Screen.height * 0.82f, (float)(Screen.width * 0.08f), (float)Screen.height * 0.06f);
            GUI.DrawTexture(container, ResourceManager.LoadTexture("Shared/blackFiller"));
            GUI.color = color;

            GUI.depth = depth - 2;

            // Draw the header
            //Rect pos = new Rect(container.x * 1.09f, container.y * 1.01f, container.width, container.height * 0.16f);
            Rect pos = new Rect(container.x * 1.09f, container.y * 1.01f, container.width, container.height * 0.50f);

            int fontSize = GUI.skin.label.fontSize;
            color = GUI.skin.label.normal.textColor;
            GUI.skin.label.fontSize = (int)((10 + Screen.height / 72) * 0.65f);
            GUI.skin.label.normal.textColor = new Color(0.85f, 0.70f, 0.043f, 1f);
            GUI.Label(pos, "ScrollsPost Replay");
            GUI.skin.label.fontSize = fontSize;
            GUI.skin.label.normal.textColor = color;

            // Start/Pause
            //pos = new Rect(container.x * 1.06f, pos.y + pos.height + 10f, container.width * 0.90f, container.height * 0.20f);
            pos = new Rect(container.x * 1.06f, pos.y + pos.height - 6f, container.width * 0.90f, container.height * 0.0f);
            if( GUI.Button(pos, paused ? "Play" : "Pause", this.buttonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");

                paused = !paused;
                if( !paused ) wasPaused = true;
            }

            // Go to Round
            //pos = new Rect(pos.x, pos.y + pos.height + 8f, pos.width, pos.height);
            //if( GUI.Button(pos, "Go To Round", this.buttonStyle) ) {
            //    App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
            //    App.Popups.ShowTextInput(this, "", "Can be a round that has always passed or one in the future.", "round", "Round Seek", "Enter a Round to Go To:", "Seek");
            //}

            // Speed changes
            /*
             * pos = new Rect(pos.x, pos.y + pos.height + 8f, pos.width * 0.32f, pos.height);

            float newSpeed = speed >= 0.50f ? speed - 0.25f : 0.25f;

            if( GUI.Button(pos, String.Format("{0}%", newSpeed * 100), this.speedButtonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                speed = newSpeed;
            }

            pos = new Rect(pos.x + pos.width + 2f, pos.y, pos.width, pos.height);
            if( GUI.Button(pos, "100%", this.speedButtonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                speed = 1;
            }

            newSpeed = speed <= 1.75f ? speed + 0.25f : 2f;

            pos = new Rect(pos.x + pos.width + 2f, pos.y, pos.width, pos.height);
            if( GUI.Button(pos, String.Format("{0}%", newSpeed * 100), this.speedButtonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                speed = newSpeed;
            }
            */

            GUI.depth = depth;
        }

        public void OnBattleUIInit(InvocationInfo info) {
            typeof(BattleModeUI).GetField("callback", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(info.target, this);
        }

        public Boolean OnBattleUIShowEndTurn(InvocationInfo info) {
            return true;
        }
        
        public Boolean OnBattleDelay(InvocationInfo info) {
            return speed > 1.5;
        }

        public void OnTweenUpdatePercentage(InvocationInfo info) {
            if( speed > 1.5 ) {
                if( (Boolean) reverseField.GetValue(info.target) ) {
                    percentField.SetValue(info.target, 0f);
                } else {
                    percentField.SetValue(info.target, 1f);
                }
            }
        }

        private void Delay(int ms) {
            // While seeking, reduce the delay
            ms = (int)Math.Round(ms * speed);

            Thread.Sleep(ms);
        }

        // Round seeking
        public void PopupCancel(String type) {

        }

        public void PopupOk(String type) {

        }

        public void PopupOk(String type, String choice) {
        }


        // Initial replay start
        private void Start() {
            Thread.Sleep(1000);

            SceneLoader.loadScene("_BattleModeView");
            reverseField = typeof(iTween).GetField("reverse", BindingFlags.NonPublic | BindingFlags.Instance);
            percentField = typeof(iTween).GetField("percentage", BindingFlags.NonPublic | BindingFlags.Instance);
            deselectMethod = typeof(BattleMode).GetMethod("deselectAllTiles", BindingFlags.Instance | BindingFlags.NonPublic);

            using( StreamReader primary = new StreamReader(this.replayPrimaryPath) ) {
                if( String.IsNullOrEmpty(this.replaySecondaryPath) ) {
                    ParseStreams(primary);
                } else {
                    using( StreamReader secondary = new StreamReader(this.replaySecondaryPath) ) {
                        ParseStreams(primary, secondary);
                    }
                }
            }
        }

        // Handle pulling lines out of the replays for parsing
        private void ParseStreams(StreamReader primary, StreamReader secondary=null) {
            // Grab the config
            String line = primary.ReadLine();
            String[] parts = line.Split(new char[] { '|' }, 2);

            metadata = new JsonReader().Read<Dictionary<String, object>>(parts[1]);
            // Sort out the IDs
            String primaryID;
            String secondaryID;
            if( primaryType.Equals("white") ) {
                primaryID = (String)metadata["white-id"];
                secondaryID = (String)metadata["black-id"];
            } else {
                primaryID = (String)metadata["black-id"];
                secondaryID = (String)metadata["white-id"];
            }

            while( primary.Peek() > 0 ) {
                line = primary.ReadLine();

                // Secondaries turn
                if( secondary != null && line.Contains("TurnBegin") && !line.Contains(primaryType) ) {
                    ParseLine(primaryID, line);
                    Delay(400);

                    String newTurn = line.Split(new char[] { '|' }, 3)[2];

                    Boolean seeking = true;

                    while( secondary.Peek() > 0 ) {
                        line = secondary.ReadLine();
                        // Find the part where the new turn is
                        if( seeking && line.Contains(newTurn) ) {
                            seeking = false;

                            // Grab the initial state change
                            ParseLine(secondaryID, secondary.ReadLine(), false);
                            Delay(1000);

                        // Turn is over, swap back to primary
                        } else if( !seeking && line.Contains("TurnBegin") ) {

                            // Now reposition us on the primary pointer
                            newTurn = line.Split(new char[] { '|' }, 3)[2];

                            while( primary.Peek() > 0 ) {
                                line = primary.ReadLine();
                                if( line.Contains(newTurn) ) {
                                    ParseLine(primaryID, line);
                                    Delay(400);

                                    // Grab the initial state change
                                    ParseLine(primaryID, primary.ReadLine(), false);
                                    Delay(1000);

                                    break;
                                }
                            }

                            break;
                        // Line during the new turn
                        } else if( !seeking ) {
                            ParseLine(secondaryID, line);
                        }
                    }

                    continue;
                }

                ParseLine(primaryID, line);
            }
        }

        // Parse a single line from the replay
        private void ParseLine(String perspectiveID, String line, Boolean delay=true) {
            // Wait until the message was read off to dump it into the queue
            while( msgPending || paused ) {
                Thread.Sleep(paused ? 100 : 10);
            }

            String[] parts = line.Split(new char[] { '|' }, 3);

            // Preserve the time taken between messages
            if( !wasPaused && delay && parts[0].Equals("elapsed") ) {
                Delay((int)Math.Round(1000 * Convert.ToDouble(parts[1])));
            }

            App.Communicator.setData(parts[2].Replace(perspectiveID, App.MyProfile.ProfileInfo.id));

            wasPaused = false;
            msgPending = true;
        }


        // Part of IBattleModeUICallback
        public Boolean allowEndTurn() {
            return false;
        }

        public void endturnPressed() {

        }

    }
}
