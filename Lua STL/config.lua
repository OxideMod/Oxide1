-- Oxide - Lua Standard Library
-- config.lua - Config library


config = {}
local configfiles = {}

function config.Read( name )
	local df = cs.getdatafile( "cfg_" .. name )
	local txt = df:GetText()
	if (txt ~= "") then
		local tbl = json.decode( txt )
		if (tbl) then
			configfiles[ name ] = tbl
			return true, tbl
		else
			error( "Corrupt config file '" .. name .. "'! Check that the json is valid." )
			return false
		end
	end
	local tbl = {}
	configfiles[ name ] = tbl
	return false, tbl
end
function config.Save( name, options )
	local tbl = configfiles[ name ]
	if (not tbl) then return false end
	local df = cs.getdatafile( "cfg_" .. name )
	if (type(options) ~= "table") then options = false end
	df:SetText( json.encode( tbl, options or { indent = true } ) )
	df:Save()
	return true
end
`
