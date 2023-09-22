
/*
 * Programmer:  Labthe3rd
 * Date:        06/11/22
 * Description: Simple Script To Play Jingle When Player Enters
 * 
 * V1.4 Update Adds More Log Messages when debug mode is enabled and error handling
 */

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

namespace Labthe3rd.PlayerEnteredSound
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerEnteredSound : UdonSharpBehaviour
    {

        [Header("Set the audio sources that will play")]
        [Tooltip("This will be the sound that plays when players joined")]
        public AudioSource[] playerEnteredAudioSources;
        [Header("Current Players In Instance Settings")]
        public bool playSound = true;
        public bool showOverlay = true;
        [Header("Joined Message Text Appears After Display Name Of Joined User")]
        public string playerJoinedMessage = "Has Joined!";
        [Header("Text Display Duration (Seconds)")]
        public float showTextDuration = 3;
        [Space]
        [Header("Internal Variables")]
        [Header("WARNING DO NOT TOUCH UNLESS YOU KNOW WHAT YOU ARE DOING")]
        [Header("Text Object To Appear On Join")]
        public TextMeshPro playerEnteredTMPObject;
        

        [Space]
        [Header("Enable Debug Mode?")]
        public bool debugMode;

        //private variables
        private VRCPlayerApi localPlayer;
        private int localPlayerID;
        //private int joinedPlayerID;
        private string joinedPlayerDisplayName;

        //private variables used for determining where to display text
        private Vector3 localHeadPosition;
        private Quaternion localHeadRotation;
        private Vector3 textDisplayPosition;
        private Quaternion textDisplayRotation;
        private string displayMessage;
        private float showTextDurationScaled;
        private float showTextBeginTime;
        private float showTextCurrentTime;

        //Used to start the hide text trigger, using the update event in case multiple players join so the text will update and restart
        private bool hideTextTrigger = false;

        //V1.4 force the TMP game object to be active to prevent user error
        private GameObject playerEnteredTMPGameObject;
        //V1.4 going to grab TMP parent object at start
        private GameObject displayGameObject;

        //V1.4 verify that joined text had displayed self once before allowing script to display text
        private bool ready = false;

        //OnPlayerJoined Events fires for joined player too for each player in the instance already.... WOW so let's do a work around
        [UdonSynced, FieldChangeCallback(nameof(JoinedPlayerID))]
        private int _joinedPlayerID;


        void Start()
        {
            playerEnteredTMPGameObject = playerEnteredTMPObject.gameObject;
            DebugMessage("Player Entered TMP Game Object Named " + playerEnteredTMPGameObject.name + " Retrieved");
            playerEnteredTMPGameObject.SetActive(true);
            DebugMessage(playerEnteredTMPGameObject.name + " Forced To Active");

            //V1.4 grabbing the parent text mesh pro object
            //displayGameObject = playerEnteredTMPGameObject.GetComponentInParent<GameObject>();
            //displayGameObject = playerEnteredTMPGameObject.GetComponentInParent<GameObject>();
            displayGameObject = playerEnteredTMPGameObject.transform.parent.gameObject;
            DebugMessage("Parent Game Object " + displayGameObject.name + " Retrieved");
            //We will hide the display message gameobject so by default to prevent operator error
            //Remove this if you intend the gameobject to be active on start
            displayGameObject.SetActive(false);
            DebugMessage(displayGameObject.name + " Set To Inactive");
            //Scale the duration selected
            showTextDurationScaled = showTextDuration * 1000f;
            DebugMessage("Show Text Duration Scaled To " + showTextDurationScaled + " mSec");

            //Local Player verification and sending hte network event
            if (Utilities.IsValid(Networking.LocalPlayer))
            {
                localPlayer = Networking.LocalPlayer;
                DebugMessage("Local Player Set To " + localPlayer.displayName);
                if (localPlayer.playerId != 0)
                {
                    localPlayerID = localPlayer.playerId;
                    DebugMessage("Local Player ID Is " + localPlayerID);

                    if(!Networking.IsClogged && Networking.IsNetworkSettled)
                    {
                        DebugMessage("Network Is Not Clogged & Network is Settled");
                        UpdatePlayerID();
                    }
                    else if (Networking.IsClogged)
                    {
                        
                        
                            DebugMessage("Network Is Clogged, Attempty To Rety In 2 Seconds");
                            SendCustomEventDelayedSeconds("RetryUpdatePlayerID", 2f);
                    }
                    else
                    {
                            DebugMessage("Network Is Not Settled, Attempting To Retry In 1 Seconds");
                            SendCustomEventDelayedSeconds("RetryUpdatePlayerID", 2f);
                    }
                    

                }
                else
                {
                    
                    DebugMessage("Local Player ID Is 0");
                }

            }
            else
            {
                
                DebugMessage("Local Player Is Not valid");
            }


        }
        
        public void RetryUpdatePlayerID()
        {
            if (!Networking.IsClogged && Networking.IsNetworkSettled)
            {
                DebugMessage("Network is no longer Clogged & Is Now Settled");
                UpdatePlayerID();
            }
            else if (Networking.IsClogged)
            {
                DebugMessage("Network Is Clogged Again, Retrying In 1 seconds");
                SendCustomEventDelayedSeconds("RetryUpdatePlayerID", 1f);
            }
            else
            {
                DebugMessage("Network Is Not Settled, Retrying In 1 Seconds");
                SendCustomEventDelayedSeconds("RetryUpdatePlayerID", 1f);
            }
        }

        private void UpdatePlayerID()
        {
            //Do not try to serialize until there is more than one player in the world
            if (VRCPlayerApi.GetPlayerCount() > 1)
            {
                if (!Networking.IsOwner(gameObject))
                {
                    DebugMessage("Player is not owner, set player to owner");
                    Networking.SetOwner(localPlayer, gameObject);
                }
                if (Networking.IsOwner(gameObject))
                {
                    //Check if joined player already displayed
                    if (JoinedPlayerID != localPlayerID)
                    {
                        JoinedPlayerID = localPlayerID;
                        DebugMessage("Joined Player ID Is: " + JoinedPlayerID + " Now Let's Request Serialization");
                        RequestSerialization();
                    }
                    else
                    {
                        DebugMessage("Do Not Display Joined Player Text, It Is A Duplicate");
                    }

                }
                else
                {
                    DebugMessage("Player Is Not Owner, Do Nothing");
                }
            }
            else
            {
                DebugMessage("Player Count Is Less Than 1");
            }
            ready = true;
            DebugMessage("Script is now ready to display players as they join");
        }

        void Update()
        {
            if (hideTextTrigger == true)
            {
                showTextCurrentTime = Networking.GetServerTimeInMilliseconds() - showTextBeginTime;
                DebugMessage("Remaining Time Is " + showTextCurrentTime + " mSec");
                if (showTextCurrentTime >= showTextDurationScaled)
                {
                    //We will hide the player joined object
                    displayGameObject.SetActive(false);
                    hideTextTrigger = false;
                    DebugMessage("Current time: " + showTextCurrentTime + " mSec > Set Duration: " + showTextDurationScaled + " Seconds\n" +
                         displayGameObject.name + " Is Now Inactive");
                }
                else
                {
                    UpdateDisplayTransform();

                }
            }

        }


        private void DisplayText()
        {

            //Next concat the string we will be displaying
            displayMessage = joinedPlayerDisplayName + " " + playerJoinedMessage;
            //Now set the TMP object's text to the message we created
            playerEnteredTMPObject.text = displayMessage;

            //Grab current server time so we can use that as our basis for the counter
            if (Utilities.IsValid(Networking.GetServerTimeInMilliseconds()))
            {
                showTextBeginTime = Networking.GetServerTimeInMilliseconds();
                DebugMessage("Display Start Time Set To " + showTextBeginTime + " mSec");
            }
            else
            {
                DebugMessage("Retrieving Server Time Was Invalid");
            }
            
            
            //Start Loop To Keep Track Of Time
            hideTextTrigger = true;
            DebugMessage("Hide Text Trigger Is Now True");

            //Show GameObject To Display
            displayGameObject.SetActive(true);
            DebugMessage(displayGameObject.name + " Is Now Active");

        }

        /*
        * We will use the players head position to set the text object and offset it by -1 in the Z direction
        * If using TMP keep the rect transform at a width of 1 and height of 1 then play with your canvas with the main camera for debug
        */
        private void UpdateDisplayTransform()
        {
            //Player's Camera is not exposed in Udon so we will use the tracking data of the head to get the view
            if (Utilities.IsValid(localPlayer))
            {
                if (Utilities.IsValid(localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position))
                {
                    localHeadPosition = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                }
                else
                {
                    localHeadPosition = new Vector3(0, 0, 0);
                    DebugMessage("Head Position Returned Invalid, setting to " + localHeadPosition);
                }

                if (Utilities.IsValid(localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation))
                {
                    localHeadRotation = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
                }
                else
                {
                    localHeadRotation = new Quaternion(0, 0, 0, 0);
                    DebugMessage("Head Rotation Returned Invalid, setting to " + localHeadRotation);

                }
                
                DebugMessage("Head Position: " + localHeadPosition + "\nHead Rotation: " + localHeadRotation);
                textDisplayPosition = localHeadPosition;
                textDisplayRotation = localHeadRotation;
                DebugMessage("Text Display Variables Postiion & Rotation Set To Head Position And Rotation");

                if (Utilities.IsValid(displayGameObject))
                {
                    displayGameObject.transform.SetPositionAndRotation(textDisplayPosition, textDisplayRotation);
                    DebugMessage("Text Transform Position & Rotation are now set!");
                }
                else
                {
                    DebugMessage("TMP Parent Object Returned As Invalid");
                }

            }
            else
            {
                DebugMessage("Local player is not valid");
            }


        }

        private void PlayerJoined()
        {
            if (Utilities.IsValid(VRCPlayerApi.GetPlayerById(JoinedPlayerID)))
            {
                joinedPlayerDisplayName = VRCPlayerApi.GetPlayerById(JoinedPlayerID).displayName;
                Debug.Log("Player Enter Sound Script's Joined Player ID Set To " + JoinedPlayerID);
                if (localPlayerID != 0)
                {
                    if (JoinedPlayerID != localPlayerID)
                    {
                        if (playSound == true)
                        {
                            if (playerEnteredAudioSources.Length > 0)
                            {
                                
                                for (int i = 0; i < playerEnteredAudioSources.Length; i++)
                                {
                                    if (Utilities.IsValid(playerEnteredAudioSources[i]))
                                    {
                                        //If forgotten to turn off loop turn it off. If you don't want this delete the line below.
                                        playerEnteredAudioSources[i].loop = false;
                                        playerEnteredAudioSources[i].Play();
                                    }
                                    else
                                    {
                                        DebugMessage("Player Audio Source " + i + " Is Valid");
                                    }

                                }
                            }
                            else
                            {
                                DebugMessage("No Player Entered Audio Sources Set");
                            }

                        }
                        else
                        {
                            Debug.Log("Play for current players is false, do not play sound");
                        }
                        if (showOverlay == true)
                        {
                            DisplayText();
                        }
                        else
                        {
                            Debug.Log("Show Text For Current Players Is False So Do Not Display");
                        }

                    }
                }
                else
                {
                    Debug.Log("Local Player's ID is 0, do not play");
                }
            }
            else
            {
                DebugMessage("Joined Player Returned Invalid, Do Not Set Joined Player ID");
            }

         }

        //If a player leaves reset the joinedplayerid value back to zero so if the same player joins it will show again
        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player))
            {
                if (player.playerId == JoinedPlayerID)
                {
                    if (Networking.IsOwner(gameObject))
                    {
                        JoinedPlayerID = 0;
                        DebugMessage("Set joined player ID to " + JoinedPlayerID);
                        RequestSerialization();
                    }
                    else
                    {
                        DebugMessage("Not game object owner, do not change Joined Player ID");
                    }
                }
                else
                {
                    DebugMessage(player.displayName + "'s ID Does Not Match So No Need To Reset");
                }
            }
            else
            {
                DebugMessage("Leaving Player Returned Invalid");
            }

        }

        private void DebugMessage(string message)
        {
            if (debugMode)
            {
                Debug.Log(gameObject.name + " Debug message: " + message);
            }
        }





        private int JoinedPlayerID
        {
            set
            {
                _joinedPlayerID = value;
                DebugMessage("Joined Player ID Is Now Set To " + _joinedPlayerID);
                if (ready)
                {
                    if (_joinedPlayerID != 0)
                    {
                        if (playSound || showOverlay)
                        {
                            if (Networking.IsOwner(gameObject) == false)
                            {
                                PlayerJoined();
                            }
                            else
                            {
                                DebugMessage("Player is owner do not run PlayerJoined method");
                            }
                        }
                        else
                        {
                            DebugMessage("Play Sound & Show Overlay Both Set To False, Do Nothing");
                        }
                    }
                    else
                    {
                        DebugMessage("Joined Player ID Is 0, Do Not Play Player Joined Event");
                    }
                }




            }
            get => _joinedPlayerID;
        }

        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {

            return true;


        }
    }
}

