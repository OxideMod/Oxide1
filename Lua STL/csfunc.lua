-- Oxide - Lua Standard Library
-- csfunc.lua - c# <-> lua interop


-- Cache globals for speed
local table = table
local oldcs = cs

local unpack = table.unpack
local rawget = rawget


function print( ... )
	local args = { ... }
	local result = ""
	for i=1, #args do
		if (i > 1) then result = result .. " " end
		result = result .. tostring( args[i] )
	end
	oldcs.print( result )
end

function error( obj )
	oldcs.error( tostring( obj ) )
end

function callunpacked( func, argtable )
	return func( unpack( argtable ) )
end

cs = {}
for k, v in pairs( oldcs ) do
	local function Wrapper( ... )
		-- http://www.lua.org/pil/8.5.html : pcall unwinds the LUA stack, xpcall doesnt
		local b, res = xpcall( v, function(err) return debug.traceback(err) end, ... )
		if (b) then return res end
		local tbl = debug.getinfo( 3, "Sl" )
		if tbl ~= nil then
			error( tbl.short_src .. ":" .. tbl.currentline .. " - A .NET exception was thrown trying to call cs." .. k .. "!" )
			util.ReportError( res )
		else
		    -- Maybe we should extend the anonymous function to push in more details using debug.info
			error( "cs."..k.." failed with no traceback: " .. tostring(res) )
			util.ReportError( res )
		end
	end
	cs[ k ] = Wrapper
end
cs.nullable = cs.gettype( "System.Nullable`1" )