/* Goodspeed Automatic Fuel Pump (c) Copyright 2014 Gaius Goodspeed

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
 * 
 * 
 * 30/04/2015 - Taken over by GPO for KSP 1.0
 * 24/01/2016 - updated for 1.0.5.  Respect resource lock, and don't operate on NO_FLOW resources.

http://www.gnu.org/licenses/gpl.html
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace GPOSpeedFuelPump
{
	public class GPOSpeedPump : PartModule
	{
		private const float Tolerance = (float)0.0001;
        
		private Dictionary<string, int> _resourceFlags;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Pump Level"), UI_FloatRange(minValue = 0f, maxValue = 16f, stepIncrement = 1f)]
		public float _pumpLevel;
        
		[KSPField (isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Pump is "), UI_Toggle (disabledText = "Off", enabledText = "On")]
		public bool _autoPump;
      
		[KSPField (isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Balance"), UI_Toggle (disabledText = "No", enabledText = "Yes")]
		public bool _autoBalance;

		public int GetResourceFlags (string resourceName, int mask)
		{
			try {
			if (_resourceFlags == null)
				_resourceFlags = new Dictionary<string, int> ();
		
			if (!_resourceFlags.ContainsKey (resourceName)) {
				string cfgValue = GameDatabase.Instance.GetConfigs ("PART").Single (c => c.name.Replace ('_', '.') == part.partInfo.name)
                                              .config.GetNodes ("MODULE").Single (n => n.GetValue ("name") == moduleName).GetValue (resourceName + "Flags");
				if (!String.IsNullOrEmpty (cfgValue)) {
					int flags;
					if (Int32.TryParse (cfgValue, out flags))
						_resourceFlags.Add (resourceName, flags);
					else
						SetResourceFlags (resourceName, -1);
				} else
					SetResourceFlags (resourceName, -1);
			}
		
			return _resourceFlags [resourceName] & mask;
			} catch (Exception e) {
				return 0;
			}
		}

		public void SetResourceFlags (string resourceName, int value)
		{
			if (_resourceFlags == null)
				_resourceFlags = new Dictionary<string, int> ();

			if (isFlowableResource (resourceName)) {
				_resourceFlags [resourceName] = value;
			} else { // don't operate on NO_FLOW resources like SolidFuel
				_resourceFlags [resourceName] = 0;
			}
		}

		public override void OnLoad (ConfigNode cn)
		{
			// KSP is booting up
			if (part.partInfo == null)
				return;
		
			foreach (PartResource pr in part.Resources) {
				string cfgValue = cn.GetValue (pr.resourceName + "Flags");
				if (!String.IsNullOrEmpty (cfgValue)) {
					int flags;
					if (Int32.TryParse (cfgValue, out flags))
						SetResourceFlags (pr.resourceName, flags);
				}
			}
		}

		public override void OnSave (ConfigNode cn)
		{
			if (_resourceFlags == null)
				return;
		
			foreach (var rf in _resourceFlags) {
				if (rf.Value != -1) {
					string flagName = rf.Key + "Flags";
					if (cn.HasValue (flagName))
						cn.SetValue (flagName, (rf.Value & 3).ToString ());
					else
						cn.AddValue (flagName, (rf.Value & 3).ToString ());
				}
			}
		}

		private bool isFlowableResource (string resourceName)
		{
			PartResourceDefinition resource = PartResourceLibrary.Instance.GetDefinition (resourceName);
			if (resource == null) {
				return false;
			}
			if (resource.resourceFlowMode == ResourceFlowMode.NO_FLOW) {
				return false;
			}
			if (resource.resourceFlowMode == ResourceFlowMode.NULL) {
				return false;
			}
			return true;
		}

		[KSPEvent (guiActive = true, guiActiveEditor = true, guiName = "Pump Options")]
		public void ConfigurePump ()
		{
			GPOSpeedPumpController.Instance().ConfigurePump(part);

		}


	}
}
