#region Header
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
//using Sandbox.Common.ObjectBuilders;
//using SpaceEngineers.Game.ModAPI;
//using SpaceEngineers.Game.ModAPI.Ingame;
//using VRage;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersScripting
{
	public class Wrapper
	{
		static void Main()
		{
			new CodeEditorEmulator().Main ("");
		}
	}


	class CodeEditorEmulator : Sandbox.ModAPI.Ingame.MyGridProgram
	{
		#endregion
		#region CodeEditor
		//Configuration
		//--------------------
		const string
			nameRemoteControl = "Remote Control";

		const double
			//angle between gravity and thrust direction that causes it to disable
			angleSafetyCutoff = 120.0 * (Math.PI / 180.0);


		//Definitions
		//--------------------

		//Opcodes for use as arguments
		//-commands may be issued directly
		const string
			command_Initialise = "Init";   //args: <none>

		//Utility definitions
		static Vector3D GridToWorld(Vector3I gridVector, IMyCubeGrid grid){
			//convert a grid vector for the specified grid into a world direction vector
			return Vector3D.Subtract (
				grid.GridIntegerToWorld (gridVector),
				grid.GridIntegerToWorld (Vector3I.Zero) );
		}

		static readonly double
			safetyCutoff = Math.Cos(angleSafetyCutoff);


		//Internal Types
		//--------------------
		public struct Status
		{
			//program data not persistent across restarts
			public bool
				initialised;

			//configuration constants
			private const char
				delimiter = ';';

			//Operations

			public void Initialise(){   //data setup
				//no data persistent across restarts
			}

			public string Store()
			{
				return "";
			}

			public bool TryRestore(string storage)
			{
				string[] elements = storage.Split(delimiter);
				return
					(elements.Length == 1);
			}
		}


		//Global variables
		//--------------------
		bool restarted = true;
		Status status;

		IMyRemoteControl remoteControl;

		List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();


		//Program
		//--------------------
		public void Main(string argument)
		{
			//First ensure the system is able to process commands
			//-if necessary, perform first time setup
			//-if necessary or requested, initialise the system
			//  >otherwise, check that the setup is still valid
			if (restarted) {
				//script has been reloaded
				//-may be first time running
				//-world may have been reloaded (or script recompiled)
				if (Storage == null) {
					//use default values
					status.Initialise();
				} else {
					//attempt to restore saved values
					//  -otherwise use defaults
					Echo ("restoring saved state...");
					if ( !status.TryRestore(Storage) ){
						Echo ("restoration failed.");
						status.Initialise();
					}
				}
				status.initialised = false; //we are not initialised after restart
				Storage = null;	//will be resaved iff initialisation is successful
				restarted = false;
			}
			if ( !status.initialised || argument == command_Initialise) {
				//if we cannot initialise, end here
				if ( !Initialise() )
					return;
			}
			else if ( !Validate() ) {
				//if saved state is not valid, try re-initialising
				//if we cannot initialise, end here
				if ( !Initialise() )
					return;
			}

			//Perform main processing
			Update ();


			//Save current status
			Storage = status.Store();
			Echo (Storage);
		}


		private void Update(){
			//-Prepare data
			//  >get local gravity vector
			//  >normalise vector to simplify angle calculation
			Vector3D
				worldGravity = remoteControl.GetNaturalGravity ();
			worldGravity.Normalize();

			//Check each thruster to see if it is safe to enable
			//-only check thrusters on our grid
			//-check that we have access to the thruster
			//-disable any that are pointing down
			//  >(with safetyCutoff tolerance)
			//  >enable any others
			GridTerminalSystem.GetBlocksOfType<IMyThrust>(temp);
			int count = 0;
			for (int i=0; i<temp.Count; i++) {
				IMyThrust thruster = (IMyThrust)temp[i];

				if (thruster.CubeGrid != remoteControl.CubeGrid)
					continue;

				count++;
				//Check if we can actually use the thruster
				if ( ValidateBlock (thruster, callbackRequired: false) ) {
					Vector3D
						worldThruster = GridToWorld (
								Base6Directions.GetIntVector(thruster.Orientation.Forward),
								thruster.CubeGrid);
					//Compare the dot product of gravity and thrust direction (normalised)
					// 1.0 => 0 degrees
					// 0.0 => 90 degrees
					//-1.0 => 180 degrees
					//safe iff angle <= safetyCutoffAngle
					//=> safe iff dot product >= cos(safetyCutoffAngle)
					bool
						safe = Vector3D.Dot(worldGravity, worldThruster) >= safetyCutoff * worldThruster.Length();
					if (thruster.Enabled != safe)
						thruster.RequestEnable (safe);
				}
			} //end for

			Echo(temp.Count.ToString() +" thrusters found.");
			Echo(count.ToString() +" processed.");
		}


		private bool Initialise()
		{
			status.initialised = false;
			Echo ("initialising...");

			//Find Remote Control
			remoteControl = null;
			GridTerminalSystem.GetBlocksOfType<IMyRemoteControl> (temp);
			for (int i=0; i < temp.Count; i++){
				if (temp[i].CustomName == nameRemoteControl) {
					if (remoteControl == null) {
						remoteControl = (IMyRemoteControl)temp[i];
					} else {
						Echo ("ERROR: duplicate name \"" +nameRemoteControl +"\"");
						return false;
					}
				}
			}
			//verify that the remote control was found
			if (remoteControl == null) {
				Echo ("ERROR: block not found \"" +nameRemoteControl +"\"");
				return false;
			}
			//validate that the remote control is operable
			if ( ! ValidateBlock (remoteControl, callbackRequired:false) ) return false;


			status.initialised = true;
			Echo ("Initialisation completed with no errors.");
			return true;
		}


		private bool ValidateBlock(IMyTerminalBlock block, bool callbackRequired=true)
		{
			//check for block deletion?

			//check that we have required permissions to control the block
			if ( ! Me.HasPlayerAccess(block.OwnerId) ) {
				Echo ("ERROR: no permissions for \"" +block.CustomName +"\"");
				return false;
			}

			//check that the block has required permissions to make callbacks
			if ( callbackRequired && !block.HasPlayerAccess(Me.OwnerId) ) {
				Echo ("ERROR: no permissions on \"" +block.CustomName +"\"");
				return false;
			}

			//check that block is functional
			if (!block.IsFunctional) {
				Echo ("ERROR: non-functional block \"" +block.CustomName +"\"");
				return false;
			}

			return true;
		}


		private bool Validate(){
			bool valid =
				ValidateBlock (remoteControl, callbackRequired: false);

			if ( !valid ) {
				Echo ("Validation of saved blocks failed.");
			}
			return valid;
		}
		#endregion
		#region footer
	}
}
#endregion