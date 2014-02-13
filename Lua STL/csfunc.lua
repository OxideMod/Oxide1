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
		local b, res = pcall( v, ... )
		if (b) then return res end
		local tbl = debug.getinfo( 3, "Sl" )
		error( tbl.short_src .. ":" .. tbl.currentline .. " - A .NET exception was thrown trying to call cs." .. k .. "!" )
		util.ReportError( res )
	end
	cs[ k ] = Wrapper
end
cs.nullable = cs.gettype( "System.Nullable`1" )