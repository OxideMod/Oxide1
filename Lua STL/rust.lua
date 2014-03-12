-- Oxide - Lua Standard Library
-- rust.lua - Provides an interface for standard rust functions

local KindType = cs.gettype( "Inventory+Slot+Kind, Assembly-CSharp" )
local KindFlagsType = cs.gettype( "Inventory+Slot+KindFlags, Assembly-CSharp" )

typesystem.LoadEnum( KindType, "InventorySlotKind" )
typesystem.LoadEnum( KindFlagsType, "InventorySlotKindFlags" )

local NetUserUserID = util.GetPropertyGetter( Rust.NetUser, "userID", true )
local userIDproperty = typesystem.TypeFromMetatype( Rust.NetUser ):GetProperty( "userID", bf.public_instance )
local DefineSlotPreferenceMethod = util.FindOverloadedMethod( cs.gettype( "Inventory+Slot+Preference, Assembly-CSharp" ), "Define", bf.public_static, { KindType, System.Boolean, KindFlagsType } )
rust = {}
local RustNoticeTemplateIcon = { Rust.NetUser, "string", "string", "number" }
function rust.NoticeIcon( netuser, text,strIcon, duration )
duration = duration or 4.0
strIcon = strIcon or "   "
if (not validate.Args( "rust.NoticeIcon", RustNoticeTemplateIcon, netuser, text,strIcon, duration )) then return end
Rust.Rust.Notice.Popup( netuser.networkPlayer, strIcon, text, duration )
end
local RustNoticeTemplate = { Rust.NetUser, "string", "number" }
function rust.Notice( netuser, text, duration )
	duration = duration or 4.0
	if (not validate.Args( "rust.Notice", RustNoticeTemplate, netuser, text, duration )) then return end
	Rust.Rust.Notice.Popup( netuser.networkPlayer, "   ", text, duration or 4.0 )
end
local RustInventoryNoticeTemplate = { Rust.NetUser, "string" }
function rust.InventoryNotice( netuser, text )
	if (not validate.Args( "rust.InventoryNotice", RustInventoryNoticeTemplate, netuser, text )) then return end
	--RustNoticePopup( netuser.networkPlayer, "   ", text, duration or 4.0 )
	Rust.Rust.Notice.Inventory( netuser.networkPlayer, text )
end
function rust.GetAllNetUsers()
	--local pclist = PlayerClientAll()
	local pclist = Rust.PlayerClient.All
	if (not pclist) then
		error( "Rust.PlayerClient.All returned nil!" )
		return
	end
	local tbl = {}
	for i=1, pclist.Count do
		tbl[i] = pclist[i - 1].netUser
	end
	return tbl
end
local RustNetUserFromNetPlayerTemplate = { uLink.NetworkPlayer }
function rust.NetUserFromNetPlayer( netplayer )
	if (not validate.Args( "rust.NetUserFromNetPlayer", RustNetUserFromNetPlayerTemplate, netplayer )) then return end
	--return NetUserFind( netplayer )
	return Rust.NetUser.Find( netplayer )
end
local RustFindNetUsersByName = { "string" }
function rust.FindNetUsersByName( name )
	if (not validate.Args( "rust.FindNetUsersByName", RustFindNetUsersByName, name )) then return end
	local allnetusers = rust.GetAllNetUsers()
	if (not allnetusers) then return false, 0 end
	local tmp = {}
	for i=1, #allnetusers do
		local netuser = allnetusers[i]
		local escapedName = string.gsub(netuser.user.Displayname, "[%-?*+%[%]%(%)]", "%%%0")
		if (netuser.user.Displayname:match( escapedName )) then
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
local RustCommunityIDToSteamID = { "number" }
function rust.CommunityIDToSteamID( id )
	if (not validate.Args( "rust.CommunityIDToSteamID", RustCommunityIDToSteamID, id )) then return end
	-- STEAM_X:Y:Z
	-- W = Z*2 + Y
	return "STEAM_0:" .. (id % 2) .. ":" .. math.floor( id / 2 )
end
local RustRunServerCommand = { "string" }
function rust.RunServerCommand( cmd )
	if (not validate.Args( "rust.RunServerCommand", RustRunServerCommand, cmd )) then return end
	--return ConsoleSystemRun( cmd )
	return Rust.ConsoleSystem.Run( cmd )
end
local RustRunClientCommand = { Rust.NetUser, "string" }
function rust.RunClientCommand( netuser, cmd )
	if (not validate.Args( "rust.RunClientCommand", RustRunClientCommand, netuser, cmd )) then return end
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
local RustGetUserID = { Rust.NetUser }
function rust.GetUserID( netuser )
	if (not validate.Args( "rust.GetUserID", RustGetUserID, netuser )) then return end
	local result = NetUserUserID( netuser )
	return tostring( result )
end
local RustGetLongUserID = { Rust.NetUser }
function rust.GetLongUserID( netuser )
	if (not validate.Args( "rust.GetLongUserID", RustGetLongUserID, netuser )) then return end
	return cs.readulongpropertyasstring( userIDproperty, netuser )
end

function rust.ServerManagement()
	--return RustServerManagementGet()
	return Rust.RustServerManagement.Get()
end
local RustGetDatablockByName = { "string" }
function rust.GetDatablockByName( name )
	if (not validate.Args( "rust.GetDatablockByName", RustGetDatablockByName, name )) then return end
	--return DatablockDictionaryGetByName( name )
	return Rust.DatablockDictionary.GetByName( name )
end
function rust.InventorySlotPreference( kind, stack, kindflags )
	return DefineSlotPreferenceMethod:Invoke( nil, util.ArrayFromTable( System.Object, { kind, stack, kindflags } ) )
end