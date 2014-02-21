--[[ ******************** ]]--
--[[ oxidecore - thomasfn ]]--
--[[ ******************** ]]--


-- Define plugin variables
PLUGIN.Title = "Oxide Core"
PLUGIN.Description = "Abstracts many hooks into a much improved API for other plugins to use"

-- Load some enums
typesystem.LoadEnum( RustFirstPass.NetError, "NetError" )
typesystem.LoadEnum( System.Reflection.BindingFlags, "BindingFlags" )
typesystem.LoadEnum( RustFirstPass.LifeStatus, "LifeStatus" )

-- Oxide version
PLUGIN.OxideVersion = "Oxide 1.10"

-- Get some other functions
local GetTakeNoDamage, SetTakeNoDamage = typesystem.GetField( RustFirstPass.TakeDamage, "takenodamage", bf.private_instance )

-- *******************************************
-- PLUGIN:Init()
-- Initialises the plugin
-- *******************************************
function PLUGIN:Init()
	-- Add console commands
	self:AddCommand( "oxide", "reloadcore", self.ccmdReload )
	self:AddCommand( "oxide", "reload", self.ccmdReloadPlugin )
	self:AddCommand( "oxide", "load", self.ccmdLoadPlugin )
	self:AddCommand( "chat", "say", self.ccmdChat )
	
	-- Add chat commands
	self:AddChatCommand( "mod", self.cmdMod )
end

-- *******************************************
-- PLUGIN:cmdChat()
-- Called when a chat command has been executed
-- *******************************************
function PLUGIN:ccmdChat( arg )
	-- Get the message
	local message = arg:GetString( 0, "text" )
	
	-- Check if it was run from the server console or not
	if (arg.argUser) then
		-- Ran from client
		local name = arg.argUser.user.Displayname
		return plugins.Call( "OnUserChat", arg.argUser, name, message )
	else
		-- Ran from server
		return plugins.Call( "OnConsoleChat", message )
	end
end

-- *******************************************
-- PLUGIN:OnUserChat()
-- Called when a user has sent a chat message
-- *******************************************
function PLUGIN:OnUserChat( netuser, name, msg )
	-- Check for 0 length message
	if (msg:len() == 0) then return end
	
	-- Is it a chat command?
	if (msg:sub( 1, 1 ) == "/") then
		-- Split into a space delimited table
		local args = {}
		for arg in msg:gmatch( "%S+" ) do
			args[ #args + 1 ] = arg
		end
		
		-- Pull the command and remove the /
		local cmd = args[1]:sub( 2 )
		
		-- Loop each argument and merge arguments surrounded by double quotes
		local newargs = {}
		local inlongarg = false
		local longarg = ""
		for i=2, #args do
			local str = args[i]
			local l = str:len()
			local handled = false
			if (l > 1) then
				if (str:sub( 1, 1 ) == "\"") then
					inlongarg = true
					longarg = longarg .. str .. " "
					handled = true
				end
				if (str:sub( l, l ) == "\"") then
					inlongarg = false
					if (not handled) then longarg = longarg .. str .. " " end
					newargs[ #newargs + 1 ] = longarg:sub( 2, longarg:len() - 2 )
					longarg = ""
					handled = true
				end
			end
			if (not handled) then
				if (inlongarg) then
					longarg = longarg .. str .. " "
				else
					newargs[ #newargs + 1 ] = str
				end
			end
		end
		
		-- Call the chat command hook
		if (not plugins.Call( "OnChatCommand", netuser, cmd, newargs )) then
			rust.Notice( netuser, "Unknown chat command!" )
		end
		
		-- Handled
		return true
	end
	
	-- Log it
	print( netuser.displayName .. ": " .. msg )
end

-- *******************************************
-- PLUGIN:OnDoorToggle()
-- Called when a user has attempted to use a door
-- *******************************************
local NullableOfVector3 = typesystem.MakeNullableOf( UnityEngine.Vector3 )
local NullableOfBoolean = typesystem.MakeNullableOf( System.Boolean )
local ToggleStateServer = util.FindOverloadedMethod( Rust.BasicDoor, "ToggleStateServer", bf.private_instance, { NullableOfVector3, System.UInt64, NullableOfBoolean } )
function PLUGIN:OnDoorToggle( door, timestamp, controllable )
	-- Sanity check
	if (not controllable) then
		local arr = util.ArrayFromTable( System.Object, { nil, timestamp, nil }, 3 )
		cs.convertandsetonarray( arr, 1, timestamp, System.UInt64._type )
		return ToggleStateServer:Invoke( door, arr )
	end
	
	-- Get the character and deployable
	local charcomponent = controllable:GetComponent( "Character" )
	local deployable = door:GetComponent( "DeployableObject" )
	local lockable = door:GetComponent( "LockableObject" )
	
	--if (((deployable != null) && deployable.BelongsTo(controllable)) || (((lockable == null) || !lockable.IsLockActive()) || lockable.HasAccess(controllable)))
	
	
	-- Let plugins decide whether to permit it or not
	local b, res = plugins.Call( "CanOpenDoor", controllable.playerClient.netUser, door )
		
	-- No output? Perform default logic
	local defaultoutput = (deployable and deployable:BelongsTo( controllable )) or (not lockable) or (not lockable:IsLockActive()) or lockable:HasAccess( controllable )
	if ((b == nil and not defaultoutput) or (b ~= nil and not b)) then
		rust.Notice( charcomponent.playerClient.netUser, res or "The door is locked!" )
		return false
	end
	
	-- Replicate the C# logic
	if (deployable) then deployable:Touched() end
	local origin
	if (charcomponent) then
		origin = charcomponent.eyesOrigin
	else
		origin = controllable.transform.position
	end
	if (type( origin ) == "string") then
		-- Let's find out what this error is
		print( "---------" )
		print( "Debugging data for oxidecore.lua:OnDoorToggle" )
		print( "origin was somehow a string!" )
		if (charcomponent) then
			print( "We tried to read eyesOrigin from charcomponent, but charcomponent is '" .. tostring( charcomponent ) .. "'" )
		else
			print( "We tried to read transform.position from controllable, but controllable is '" .. tostring( controllable ) .. "' and controllable.transform is '" .. tostring( controllable.transform ) .. "'" )
		end
		print( "Please submit this data to the Oxide developers!" )
		print( "---------" )
		
		-- Not handled
		return
	end
	local arr = util.ArrayFromTable( System.Object, { new( NullableOfVector3, origin ), timestamp, nil }, 3 )
	cs.convertandsetonarray( arr, 1, timestamp, System.UInt64._type )
	return ToggleStateServer:Invoke( door, arr )
		
	-- Handled
	--return true
end

-- *******************************************
-- PLUGIN:OnProcessDamageEvent()
-- Called when it's time to process a damage event
-- *******************************************
local StatusIntGetter = util.GetFieldGetter( RustFirstPass.DamageEvent, "status", nil, System.Int32 )
local LifeStatus_IsAlive = 0
local LifeStatus_IsDead = 2
local LifeStatus_WasKilled = 1
local LifeStatus_Failed = -1
function PLUGIN:OnProcessDamageEvent( takedamage, damage )
	if (GetTakeNoDamage( takedamage )) then return true end
	local status = StatusIntGetter( damage )
	--print( "==========" )
	--print( status )
	if (status == LifeStatus_WasKilled) then
		--print( "setting health to 0!" )
		takedamage.health = 0
		plugins.Call( "OnKilled", takedamage, damage )
	elseif (status == LifeStatus_IsAlive) then
		--print( "reducing health!" )
		--print( takedamage.health )
		takedamage.health = takedamage.health - damage.amount
		--print( takedamage.health )
		plugins.Call( "OnHurt", takedamage, damage )
	end
	return true
end

-- *******************************************
-- PLUGIN:OnAirdrop()
-- Called when an airdrop has been initiated
-- *******************************************
function PLUGIN:OnAirdrop( pos )
	if (self.BypassAirdropHook) then return end
	return plugins.Call( "ShouldAirdrop", pos )
end

-- *******************************************
-- PLUGIN:CanClientLogin()
-- Called when a user attempts to login
-- *******************************************
local blacklist =
{
	Oxide = true,
	Oxmin = true
}
function PLUGIN:CanClientLogin( login )
	local steamlogin = login.SteamLogin
	if (blacklist[ steamlogin.UserName ]) then return NetError.Facepunch_Kick_BadName end
end

-- *******************************************
-- PLUGIN:OnSteamGetTags()
-- Called when the server wants to know the tags
-- *******************************************
function PLUGIN:OnSteamGetTags()
	local tbl = {}
	plugins.Call( "ModifyServerTags", tbl )
	local tags = table.concat( tbl, "," )
	return tags
end

-- *******************************************
-- PLUGIN:OnSteamGetTags()
-- Called when it's time to build the tags table
-- *******************************************
function PLUGIN:ModifyServerTags( tags )
	table.insert( tags, "rust" )
	table.insert( tags, "modded" )
	table.insert( tags, "oxide" )
end

-- *******************************************
-- PLUGIN:ccmdReload()
-- Called when the user executes "oxide.reloadcore"
-- *******************************************
function PLUGIN:ccmdReload( arg )
	local user = arg.argUser
	if (user and not user:CanAdmin()) then return end
	print( "Reloading oxide core..." )
	plugins.Reload( self.Name )
end

-- *******************************************
-- PLUGIN:ccmdReloadPlugin()
-- Called when the user executes "oxide.reload"
-- *******************************************
function PLUGIN:ccmdReloadPlugin( arg )
	local user = arg.argUser
	if (user and not user:CanAdmin()) then return end
	print( "Reloading oxide plugin..." )
	plugins.Reload( arg:GetString( 0, "text" ) )
end

-- *******************************************
-- PLUGIN:ccmdLoadPlugin()
-- Called when the user executes "oxide.load"
-- *******************************************
function PLUGIN:ccmdLoadPlugin( arg )
	local user = arg.argUser
	if (user and not user:CanAdmin()) then return end
	print( "Loading oxide plugin..." )
	plugins.Load( arg:GetString( 0, "text" ) )
end

-- *******************************************
-- PLUGIN:cmdMod()
-- Called when the user executes the "/mod" chat command
-- *******************************************
function PLUGIN:cmdMod( netuser, cmd, args )
	rust.SendChatToUser( netuser, "This server is running " .. self.OxideVersion .. "." )
end

-- *******************************************
-- rust.CallAirdrop()
-- Calls an airdrop
-- *******************************************
--local SupplyDropZoneCallAirDrop = static( Rust.SupplyDropZone, "CallAirDrop" )
--local SupplyDropZoneCallAirDropAt = static( Rust.SupplyDropZone, "CallAirDropAt" )
function rust.CallAirdrop( pos )
	local oxidecore = plugins.Find( "oxidecore" )
	oxidecore.BypassAirdropHook = true
	if (pos) then
		--SupplyDropZoneCallAirDropAt( pos )
		Rust.SupplyDropZone.CallAirDropAt( pos )
	else
		--SupplyDropZoneCallAirDrop()
		Rust.SupplyDropZone.CallAirDrop()
	end
	oxidecore.BypassAirdropHook = false
end
