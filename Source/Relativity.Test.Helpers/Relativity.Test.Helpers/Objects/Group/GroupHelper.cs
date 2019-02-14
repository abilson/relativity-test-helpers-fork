﻿using kCura.Relativity.Client;
using kCura.Relativity.Client.DTOs;
using DTOs = kCura.Relativity.Client.DTOs;
using Relativity.Services.Group;
using Relativity.Services.Permission;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Relativity.Test.Helpers.Objects.Group
{
	public class GroupHelper
	{
		private TestHelper _helper;
		public GroupHelper(TestHelper helper)
		{
			_helper = helper;
		}

		public int CreateGroup(String name)
		{
			DTOs.Group newGroup = new DTOs.Group();
			newGroup.Name = name;
			WriteResultSet<DTOs.Group> resultSet = new WriteResultSet<DTOs.Group>();

			try
			{
				using (var client = _helper.GetServicesManager().CreateProxy<IRSAPIClient>(API.ExecutionIdentity.System))
				{
					client.APIOptions.WorkspaceID = -1;
					resultSet = client.Repositories.Group.Create(newGroup);
				}
				if (!resultSet.Success || resultSet.Results.Count == 0)
				{
					throw new Exception("Group was not found");
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Exception occured while trying to create a new group" + e);
			}
			var groupartid = resultSet.Results[0].Artifact.ArtifactID;
			return groupartid;
		}

		public bool DeleteGroup(int artifactId)
		{
			var groupToDelete = new DTOs.Group(artifactId);
			WriteResultSet<DTOs.Group> resultSet = new WriteResultSet<DTOs.Group>();

			try
			{
				using (var client = _helper.GetServicesManager().CreateProxy<IRSAPIClient>(API.ExecutionIdentity.System))
				{
					resultSet = client.Repositories.Group.Delete(groupToDelete);
				}

				if (!resultSet.Success || resultSet.Results.Count == 0)
				{
					throw new Exception("Group not found in Relativity");
				}

			}
			catch (Exception e)
			{
				Console.WriteLine("Group deletion threw an exception" + e);
				return false;
			}

			return true;
		}

		public bool AddGroupToWorkspace(Int32 eddsWorkspaceArtifactID, kCura.Relativity.Client.DTOs.Group group)
		{
			bool success = false;

			GroupSelector groupSelector;

			using (var permissionManager = _helper.GetServicesManager().CreateProxy<IPermissionManager>(API.ExecutionIdentity.System))
			{
				groupSelector = permissionManager.GetWorkspaceGroupSelectorAsync(eddsWorkspaceArtifactID).Result;

			}
			GroupRef groupRef = groupSelector.DisabledGroups.FirstOrDefault(x => x.Name == group.Name);
			if (groupRef != null)
			{
				GroupSelector modifyGroupSelector = new GroupSelector() { LastModified = groupSelector.LastModified };
				modifyGroupSelector.EnabledGroups.Add(groupRef);
				using (var permissionManager = _helper.GetServicesManager().CreateProxy<IPermissionManager>(API.ExecutionIdentity.System))
				{
					permissionManager.AddRemoveWorkspaceGroupsAsync(eddsWorkspaceArtifactID, modifyGroupSelector).Wait();
				}
				success = true;
			}

			return success;
		}

		public bool RemoveGroupFromWorkspace(Int32 eddsWorkspaceArtifactID, kCura.Relativity.Client.DTOs.Group group)
		{
			bool success = false;
			GroupSelector groupSelector;
			using (var permissionManager = _helper.GetServicesManager().CreateProxy<IPermissionManager>(API.ExecutionIdentity.System))
			{
				groupSelector = permissionManager.GetWorkspaceGroupSelectorAsync(eddsWorkspaceArtifactID).Result;
			}
			GroupRef groupRef = groupSelector.EnabledGroups.FirstOrDefault(x => x.Name == group.Name);
			if (groupRef != null)
			{
				GroupSelector modifyGroupSelector = new GroupSelector() { LastModified = groupSelector.LastModified };
				modifyGroupSelector.DisabledGroups.Add(groupRef);
				using (var permissionManager = _helper.GetServicesManager().CreateProxy<IPermissionManager>(API.ExecutionIdentity.System))
				{
					permissionManager.AddRemoveWorkspaceGroupsAsync(eddsWorkspaceArtifactID, modifyGroupSelector).Wait();
				}
				success = true;
			}

			return success;
		}

		/// <summary>
		/// Secures an artifact in a workspace from a group that is specified.
		/// The group and workspace must be setup before hand
		/// </summary>
		/// <param name="workspaceId">The id of the workspace that contains the group and artifact that should be secured</param>
		/// <param name="groupName">The group that you want to artifact secured from</param>
		/// <param name="artifactId">The id of the artifact that should be secured</param>
		private async Task SecureItemFromGroupAsync(int workspaceId, string groupName, int artifactId)
		{
			ItemLevelSecurity itemSecurity;
			using (var permissionManager = _helper.GetServicesManager().CreateProxy<IPermissionManager>(API.ExecutionIdentity.System))
			{
				 itemSecurity = await permissionManager.GetItemLevelSecurityAsync(workspaceId, artifactId).ConfigureAwait(false);
			}
				
			if (!itemSecurity.Enabled)
			{
				itemSecurity.Enabled = true;
				using (var permissionManager = _helper.GetServicesManager().CreateProxy<IPermissionManager>(API.ExecutionIdentity.System))
				{
					await permissionManager.SetItemLevelSecurityAsync(workspaceId, itemSecurity).ConfigureAwait(false);
				}
			}
			GroupSelector selection;
			using (var permissionManager = _helper.GetServicesManager().CreateProxy<IPermissionManager>(API.ExecutionIdentity.System))
			{
				selection = await permissionManager.GetItemGroupSelectorAsync(workspaceId, artifactId).ConfigureAwait(false);
			}
			foreach (var enabledGroup in selection.EnabledGroups)
			{
				if (enabledGroup.Name == groupName)
				{
					selection.DisabledGroups.Add(enabledGroup);
					selection.EnabledGroups.Add(enabledGroup);
					using (var permissionManager = _helper.GetServicesManager().CreateProxy<IPermissionManager>(API.ExecutionIdentity.System))
					{
						await permissionManager.AddRemoveItemGroupsAsync(workspaceId, artifactId, selection).ConfigureAwait(false);
					}
					break;
				}
			}
		}

		/// <summary>
		/// Secures artifacts in a workspace from a group that is specified.
		/// The group and workspace must be setup before hand
		/// </summary>
		/// <param name="mgr"></param>
		/// <param name="workspaceId">The id of the workspace that contains the group and artifact that should be secured</param>
		/// <param name="groupName">The group that you want to artifact secured from</param>
		/// <param name="artifactIds">The ids of the artifacts that should be secured</param>
		public void SecureItemFromGroup(IPermissionManager mgr, int workspaceId, string groupName, IEnumerable<int> artifactIds)
		{
			var tasks = new List<Task>();
			foreach (var artifactId in artifactIds)
			{
				tasks.Add(SecureItemFromGroupAsync(workspaceId, groupName, artifactId));
			}
			Task.WaitAll(tasks.ToArray());
		}
	}
}
