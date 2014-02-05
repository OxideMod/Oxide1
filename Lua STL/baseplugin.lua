-- Oxide - Lua Standard Library
-- baseplugin.lua - All plugins inherit this one

local BASE = {}
local metaPlugin = {}
metaPlugin.__index = BASE
BasePlugin = BASE

function BASE:BaseInit()
	self.Commands = {}
	self.ChatCommands = {}
end
function BASE:AddCommand( class, name, callback )
	local data = {}
	data.Class = class
	data.Name = name
	data.Callback = callback
	self.Commands[ class .. "." .. name ] = data
end
function BASE:AddChatCommand( cmd, callback )
	self.ChatCommands[ cmd ] = callback
end
function BASE:OnRunCommand( arg )
	local cmd = arg.Class .. "." .. arg.Function
	if (self.Commands[ cmd ]) then
		local data = self.Commands[ cmd ]
		local b, res = pcall( data.Callback, self, arg )
		if (not b) then
			arg:ReplyWith( "Lua error handling command: " .. tostring( res ) )
			error( "Lua error handling console command '" .. cmd .. "'!" )
			util.ReportError( res )
		else
			if (res ~= nil) then return res end
		end
	end
end
function BASE:OnChatCommand( netuser, cmd, args )
	if (self.ChatCommands[ cmd ]) then
		local func = self.ChatCommands[ cmd ]
		local b, res = pcall( func, self, netuser, cmd, args )
		if (not b) then
			rust.SendChatToUser( netuser, "Uh oh - a Lua error occured while running that command!" )
			error( "Lua error handling chat command '" .. cmd .. "'!" )
			util.ReportError( res )
		end
		return true
	end
end
function createplugin()
	local o = {}
	setmetatable( o, metaPlugin )
	o:BaseInit()
	return o
end