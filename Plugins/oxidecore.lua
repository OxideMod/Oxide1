--[[ ******************** ]]--
--[[ oxidecore - thomasfn ]]--
--[[ ******************** ]]--


-- Define plugin variables
PLUGIN.Title = "Oxide Core"
PLUGIN.Description = "Abstracts many hooks into a much improved API for other plugins to use"
PLUGIN.Author = "thomasfn"
PLUGIN.Version = "1.16"

-- Load some enums
typesystem.LoadEnum( Rust.NetError, "NetError" )
typesystem.LoadEnum( uLink.NetworkConnectionError, "NetworkConnectionError" )
typesystem.LoadEnum( System.Reflection.BindingFlags, "BindingFlags" )
typesystem.LoadEnum( Rust.LifeStatus, "LifeStatus" )

-- Oxide version
PLUGIN.OxideVersion = "Oxide 1.16"
PLUGIN.RustProtocolVersion = 0x42d

-- Get some other functions
local GetTakeNoDamage, SetTakeNoDamage = typesystem.GetField( Rust.TakeDamage, "takenodamage", bf.private_instance )
local GetEyesOrigin, SetEyesOrigin = typesystem.GetField( Rust.Character, "eyesOrigin", bf.public_instance )

-- *******************************************
-- PLUGIN:Init()
-- Initialises the plugin
-- *******************************************
function PLUGIN:Init()
	-- Add console commands
	self:AddCommand( "oxide", "reloadcore", self.ccmdReload )
	self:AddCommand( "oxide", "reload", self.ccmdReloadPlugin )
	self:AddCommand( "chat", "say", self.ccmdChat )
	
	-- Add chat commands
	self:AddChatCommand( "mod", self.cmdMod )
	self:AddChatCommand( "version", self.cmdMod )
	
	-- Declare tables
	self.UserDict = {}
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
-- PLUGIN:OnSpawnPlayer()
-- Called when a player spawns
-- *******************************************
function PLUGIN:OnSpawnPlayer( playerclient, usecamp, avatar )
	timer.NextFrame( function() self:HandleSpawn( playerclient ) end )
end
function PLUGIN:HandleSpawn( playerclient )
	local controllable = playerclient.controllable
	local char = controllable:GetComponent( "Character" )
	local inv = controllable:GetComponent( "Inventory" )
	--print( "[" .. char:GetType().Name .. "] " .. tostring( char ) )
	--print( "[" .. inv:GetType().Name .. "] " .. tostring( inv ) )
	local data = {}
	data.inv = inv
	data.char = char
	self.UserDict[ playerclient.netUser ] = data
end

-- *******************************************
-- PLUGIN:OnUserDisconnect()
-- Called when a player disconnects
-- *******************************************
function PLUGIN:OnUserDisconnect( networkplayer )
	local netuser = networkplayer:GetLocalData()
	if (not netuser or netuser:GetType().Name ~= "NetUser") then return end
	self.UserDict[ netuser ] = nil
	--print( "OnUserDisconnect: " .. tostring( netuser ) )
end

-- *******************************************
-- PLUGIN:OnDoorToggle()
-- Called when a user has attempted to use a door
-- *******************************************
local NullableOfVector3 = typesystem.MakeNullableOf( UnityEngine.Vector3 )
local NullableOfBoolean = typesystem.MakeNullableOf( System.Boolean )
local ToggleStateServer = util.FindOverloadedMethod( Rust.BasicDoor, "ToggleStateServer", bf.private_instance, { NullableOfVector3, System.UInt64, NullableOfBoolean } )
local GetEyesOrigin, SetEyesOrigin = typesystem.GetProperty( Rust.Character, "eyesOrigin", bf.public_instance )
function PLUGIN:OnDoorToggle( door, timestamp, controllable )
	-- Sanity check
	if (not controllable) then
		local arr = util.ArrayFromTable( System.Object, { nil, timestamp, nil }, 3 )
		cs.convertandsetonarray( arr, 1, timestamp, System.UInt64._type )
		return ToggleStateServer:Invoke( door, arr )
	end
	
	-- Get the character and deployable
	--local charcomponent = controllable:GetComponent( "Character" )
	local netuser = controllable.playerClient.netUser
	if (not netuser) then return error( "Failed to get net user (OnDoorToggle)" ) end
	local charcomponent = rust.GetCharacter( netuser )
	if (not charcomponent) then return error( "Failed to get Character (OnDoorToggle)" ) end
	--local ct = charcomponent:GetType()
	--print( ct )
	--[[if (ct.Name == "DamageBeing") then
		charcomponent = charcomponent.character
		print( "Hacky fix, " .. ct.Name .. " is now " .. charcomponent:GetType().Name )
		if (charcomponent:GetType().Name == "DamageBeing") then
			print( "The hacky fix didn't work, it's still a DamageBeing!" )
			return
		end
	end]]
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
		if (type( charcomponent.eyesOrigin ) == "string") then
			print( "eyesOrigin was a string! (charcomponent = " .. tostring( charcomponent ) .. " - " .. (charcomponent and charcomponent:GetType().Name or "") .. ")" )
			origin = GetEyesOrigin( charcomponent )
		else
			
		end
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
local StatusIntGetter = util.GetFieldGetter( Rust.DamageEvent, "status", nil, System.Int32 )
local LifeStatus_IsAlive = 0
local LifeStatus_IsDead = 2
local LifeStatus_WasKilled = 1
local LifeStatus_Failed = -1
function PLUGIN:OnProcessDamageEvent( takedamage, damage )
	damage = plugins.Call( "ModifyDamage", takedamage, damage ) or damage
	if (GetTakeNoDamage( takedamage )) then return damage end
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
	return damage
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
-- PLUGIN:OnUserApprove()
-- Called when a user attempts to login
-- *******************************************
local BanListContains = util.FindOverloadedMethod( Rust.BanList, "Contains", bf.public_static, { System.UInt64 } )
local ConnectionUserID = typesystem.GetField( Rust.ClientConnection, "UserID", bf.public_instance, true )
local AcceptorIsConnected = util.FindOverloadedMethod( Rust.ConnectionAcceptor, "IsConnected", bf.public_instance, { System.UInt64 } )
--print( BanListContains )
--print( ConnectionUserID )
--print( AcceptorIsConnected )
function PLUGIN:OnUserApprove( acceptor, approval )
	--print( "OnUserApprove" )
	if (acceptor.m_Connections.Count >= Rust.server.maxplayers) then
		--print( "Too many players" )
		approval:Deny( NetworkConnectionError.TooManyConnectedPlayers )
		return true
	end
	local item = new( Rust.ClientConnection )
	--print( approval.loginData )
	--print( item )
	local val = item:ReadConnectionData( approval.loginData )
	--print( val )
	if (not val) then
		approval:Deny( NetworkConnectionError.IncorrectParameters )
		print( "Denying entry to client with invalid parameters" )
		return true
	end
	if (item.Protocol < self.RustProtocolVersion) then
		print( "Denying entry to client with invalid protocol version (" .. approval.ipAddress .. ")" )
		approval:Deny( NetworkConnectionError.IncompatibleVersions )
		return true
	end
	local arr = newarray( System.Object, 1 )
	util.ArraySetFromField( arr, 0, ConnectionUserID, item )
	if (BanListContains:Invoke( nil, arr )) then
		print( "Rejecting client (" + tostring( item.UserID ) + " in banlist)" )
		approval:Deny( NetworkConnectionError.ConnectionBanned )
		return true
	end
	if (AcceptorIsConnected:Invoke( acceptor, arr )) then
		print( "Denying entry to " + tostring( item.UserID ) + " because they're already connected" )
		approval:Deny( NetworkConnectionError.AlreadyConnectedToAnotherServer )
		return true
	end
	local tmp = plugins.Call( "CanClientLogin", approval, item )
	if (tmp) then
		--print( "CanClientLogin said no" )
		approval:Deny( tmp )
		return true
	end
	--print( "Starting coroutine!" )
	acceptor.m_Connections:Add( item )
	acceptor:StartCoroutine( item:AuthorisationRoutine( approval ) )
	approval:Wait()
	
	return true
	
	--[[
    if (this.m_Connections.Count >= server.maxplayers)
    {
        approval.Deny(NetworkConnectionError.TooManyConnectedPlayers);
    }
    else
    {
        ClientConnection item = new ClientConnection();
        if (!item.ReadConnectionData(approval.loginData))
        {
            approval.Deny(NetworkConnectionError.IncorrectParameters);
        }
        else if (item.Protocol < 0x42b)
        {
            Debug.Log("Denying entry to client with invalid protocol version (" + approval.ipAddress + ")");
            approval.Deny(NetworkConnectionError.IncompatibleVersions);
        }
        else if (BanList.Contains(item.UserID))
        {
            Debug.Log("Rejecting client (" + item.UserID.ToString() + "in banlist)");
            approval.Deny(NetworkConnectionError.ConnectionBanned);
        }
        else if (this.IsConnected(item.UserID))
        {
            Debug.Log("Denying entry to " + item.UserID.ToString() + " because they're already connected");
            approval.Deny(NetworkConnectionError.AlreadyConnectedToAnotherServer);
        }
        else
        {
            this.m_Connections.Add(item);
            base.StartCoroutine(item.AuthorisationRoutine(approval));
            approval.Wait();
        }
    }
	]]
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
function PLUGIN:CanClientLogin( approval, connection )
	if (blacklist[ connection.UserName ]) then return NetworkConnectionError.ApprovalDenied end
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
	if (user and not user:CanAdmin()) then return true end
	print( "Reloading core..." )
	local result = plugins.Reload( self.Name )
	if (not result) then print( "Reload failed." ) end
	return true
end

-- *******************************************
-- PLUGIN:ccmdReloadPlugin()
-- Called when the user executes "oxide.reload"
-- *******************************************
function PLUGIN:ccmdReloadPlugin( arg )
	local user = arg.argUser
	if (user and not user:CanAdmin()) then return true end
	local name = arg:GetString( 0, "text" )
	print( "Reloading oxide plugin '" .. name .. "'..." )
	local result = plugins.Reload( name )
	if (not result) then print( "Reload failed." ) end
	return true
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

function rust.GetInventory( netuser )
	local core = plugins.Find( "oxidecore" )
	local data = core.UserDict[ netuser ]
	if (data and data.inv) then return data.inv end
end
function rust.GetCharacter( netuser )
	local core = plugins.Find( "oxidecore" )
	local data = core.UserDict[ netuser ]
	if (data and data.char) then return data.char end
end
