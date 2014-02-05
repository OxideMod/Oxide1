-- Oxide - Lua Standard Library
-- rust.lua - Provides an interface for standard rust functions

local KindType = cs.gettype( "Inventory+Slot+Kind, Assembly-CSharp" )
local KindFlagsType = cs.gettype( "Inventory+Slot+KindFlags, Assembly-CSharp" )

typesystem.LoadEnum( KindType, "InventorySlotKind" )
typesystem.LoadEnum( KindFlagsType, "InventorySlotKindFlags" )

local NetUserUserID = util.GetPropertyGetter( RustFirstPass.NetUser, "userID", true )
local userIDproperty = typesystem.TypeFromMetatype( RustFirstPass.NetUser ):GetProperty( "userID", bf.public_instance )
local DefineSlotPreferenceMethod = util.FindOverloadedMethod( cs.gettype( "Inventory+Slot+Preference, Assembly-CSharp" ), "Define", bf.public_static, { KindType, System.Boolean, KindFlagsType } )
rust = {}
local RustNoticeTemplate = { RustFirstPass.NetUser, "string", "number" }
function rust.Notice( netuser, text, duration )
	duration = duration or 4.0
	if (not validate.Args( "rust.Notice", RustNoticeTemplate, netuser, text, duration )) then return end
	Rust.Rust.Notice.Popup( netuser.networkPlayer, "   ", text, duration or 4.0 )
end
local RustInventoryNoticeTemplate = { RustFirstPass.NetUser, "string" }
function rust.InventoryNotice( netuser, text )
	if (not validate.Args( "rust.InventoryNotice", RustInventoryNoticeTemplate, netuser, text )) then return end
	--RustNoticePopup( netuser.networkPlayer, "   ", text, duration or 4.0 )
	Rust.Rust.Notice.Inventory( netuser.networkPlayer, text )
end
function rust.GetAllNetUsers()
	--local pclist = PlayerClientAll()
	local pclist = RustFirstPass.PlayerClient.All
	if (not pclist) then
		error( "RustFirstPass.PlayerClient.All returned nil!" )
		return
	end
	local tbl = {}
	for i=1, pclist.Count do
		tbl[i] = pclist[i - 1].netUser
	end
	return tbl
end
function rust.NetUserFromNetPlayer( netplayer )
	--return NetUserFind( netplayer )
	return RustFirstPass.NetUser.Find( netplayer )
end
function rust.FindNetUsersByName( name )
	local allnetusers = rust.GetAllNetUsers()
	if (not allnetusers) then return false, 0 end
	local tmp = {}
	for i=1, #allnetusers do
		local netuser = allnetusers[i]
		if (netuser.user.Displayname:match( name )) then
			tmp[ #tmp + 1 ] = netuser
		end
	end
	if (#tmp == 0) then return false, 0 end
	if (#tmp > 1) then return false, #tmp end
	return true, tmp[1]
end
function rust.BroadcastChat( arg1, arg2 )
	if (arg2) then
		--ConsoleNetworkerBroadcast( "chat.add \"" .. util.QuoteSafe( arg1 ) .. "\" \"" .. util.QuoteSafe( arg2 ) .. "\"" )
		Rust.ConsoleNetworker.Broadcast( "chat.add \"" .. util.QuoteSafe( arg1 ) .. "\" \"" .. util.QuoteSafe( arg2 ) .. "\"" )
	else
		--ConsoleNetworkerBroadcast( "chat.add \"Oxide\" \"" .. util.QuoteSafe( arg1 ) .. "\"" )
		Rust.ConsoleNetworker.Broadcast( "chat.add \"Oxide\" \"" .. util.QuoteSafe( arg1 ) .. "\"" )
	end
end
function rust.CommunityIDToSteamID( id )
	-- STEAM_X:Y:Z
	-- W = Z*2 + Y
	return "STEAM_0:" .. (id % 1) .. ":" .. math.floor( id / 2 )
end
function rust.RunServerCommand( cmd )
	--return ConsoleSystemRun( cmd )
	return RustFirstPass.ConsoleSystem.Run( cmd )
end
function rust.RunClientCommand( netuser, cmd )
	--ConsoleNetworkerSendClientCommand( netuser.networkPlayer, cmd )
	Rust.ConsoleNetworker.SendClientCommand( netuser.networkPlayer, cmd )
end
function rust.SendChatToUser( netuser, arg1, arg2 )
	if (arg2) then
		--ConsoleNetworkerSendClientCommand( netuser.networkPlayer, "chat.add \"" .. util.QuoteSafe( arg1 ) .. "\" \"" .. util.QuoteSafe( arg2 ) .. "\"" )
		Rust.ConsoleNetworker.SendClientCommand( netuser.networkPlayer, "chat.add \"" .. util.QuoteSafe( arg1 ) .. "\" \"" .. util.QuoteSafe( arg2 ) .. "\"" )
	else
		--ConsoleNetworkerSendClientCommand( netuser.networkPlayer, "chat.add \"Oxide\" \"" .. util.QuoteSafe( arg1 ) .. "\"" )
		Rust.ConsoleNetworker.SendClientCommand( netuser.networkPlayer, "chat.add \"Oxide\" \"" .. util.QuoteSafe( arg1 ) .. "\"" )
	end
end
function rust.GetUserID( netuser )
	local result = NetUserUserID( netuser )
	return tostring( result )
end
function rust.GetLongUserID( netuser )
	return cs.readulongpropertyasstring( userIDproperty, netuser )
end

function rust.ServerManagement()
	--return RustServerManagementGet()
	return Rust.RustServerManagement.Get()
end
function rust.GetDatablockByName( name )
	--return DatablockDictionaryGetByName( name )
	return Rust.DatablockDictionary.GetByName( name )
end
function rust.InventorySlotPreference( kind, stack, kindflags )
	return DefineSlotPreferenceMethod:Invoke( nil, util.ArrayFromTable( System.Object, { kind, stack, kindflags } ) )
end