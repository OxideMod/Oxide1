-- Oxide - Lua Standard Library
-- type.lua - Type system

local rawget = rawget
local rawset = rawset

typesystem = {}

local metaType = {}
function metaType.__index( t, k )
	-- This is the index metamethod for a namespace'd type
	-- eg: RustFirstPass.NetUser[ k ]
	-- We should look for static methods, properties and fields
	
	-- Check if we have a property by this name
	local props = rawget( t, "_properties" )
	local property = props[ k ]
	if (property) then return property:GetValue( nil, nil ) end
	
	-- Check if we have a field by this name
	local fields = rawget( t, "_fields" )
	local field = fields[ k ]
	if (field) then return field:GetValue( nil ) end
	
	-- Check if we have a method by this name
	local methods = rawget( t, "_methods" )
	local method = methods[ k ]
	if (method) then return method end
	
	-- Get the .net type
	--local typ = rawget( t, "_type" )
	
	-- Get all static methods that match the key
	--[[local arr = typ:GetMethods( bf.public_static )
	local tbl = {}
	for i=0, arr.Length do
		local method = arr[i]
		if (method.Name == k) then
			tbl[ #tbl + 1 ] = method
		end
	end
	
	-- Check how many results we have
	if (#tbl > 1) then
		-- It's an overload, so just store/return a table of methods
		rawset( t, k, tbl )
		return tbl
	elseif (#tbl == 1) then
		-- Create a caller
		local method = tbl[1]
		local function Caller( ... )
			return method:Invoke( nil, util.ArrayFromTable( { ... } ) )
		end
		
		-- Store and return the method
		rawset( t, k, Caller )
		return Caller
	end
	
	-- Look up all properties
	arr = rawget( t, "_allproperties" )
	tbl = {}
	for i=0, arr.Length do
		local property = arr[i]
		if (property.Name == k) then
			-- Store it
			props[ k ] = property
			return property:GetValue( nil, nil )
		end
	end
	
	-- Look up all fields
	arr = rawget( t, "_allfields" )
	tbl = {}
	for i=0, arr.Length do
		local field = arr[i]
		if (field.Name == k) then
			-- Store it
			fields[ k ] = field
			return field:GetValue( nil )
		end
	end]]
end
function metaType.__newindex( t, k, v )
	-- This is the newindex metamethod for a namespace'd type
	-- eg: RustFirstPass.NetUser[ k ] = v
	-- We should look for static properties and fields
	
	-- Check if we have a property by this name
	local props = rawget( t, "_properties" )
	local property = props[ k ]
	if (property) then return property:SetValue( nil, v, nil ) end
	
	-- Check if we have a field by this name
	local fields = rawget( t, "_fields" )
	local field = fields[ k ]
	if (field) then return field:SetValue( nil, v ) end
end

local function ParameterIsUseful( pinfo )
	return not pinfo.IsOut and not pinfo.IsIn
end
local function ParametersAreUseful( pinfoarr )
	for i=0, pinfoarr.Length - 1 do
		if (not ParameterIsUseful( pinfoarr[ i ] )) then return false end
	end
	return true
end

function typesystem.MakeType( typ )
	-- Create the table and set the table
	local o = {}
	o._type = typ
	
	-- Read all properties
	o._properties = {}
	local props = typ:GetProperties( bf.public_static )
	for i=0, props.Length - 1 do
		local prop = props[i]
		o._properties[ prop.Name ] = prop
	end
	
	-- Read all fields
	o._fields = {}
	local fields = typ:GetFields( bf.public_static )
	for i=0, fields.Length - 1 do
		local field = fields[i]
		o._fields[ field.Name ] = field
	end
	
	-- Read all methods
	o._methods = {}
	local methods = typ:GetMethods( bf.public_static )
	for i=0, methods.Length - 1 do
		-- Get the method
		local method = methods[i]
		
		-- Verify we can actually use it from Lua
		local plist = method:GetParameters()
		if (ParametersAreUseful( plist )) then
			-- Check for an existing method
			local existing = o._methods[ method.Name ]
			if (existing) then
				if (type( existing ) == "table") then
					existing[ #existing + 1 ] = method
				else
					local tmp = {}
					o._methods[ method.Name ] = tmp
					tmp[1] = existing
					tmp[2] = method
				end
			else
				--[[local function InvokeMethod( method, arr )
					return method:Invoke( nil, arr )
				end]]
				cs.registerstaticmethod( "tmp", method )
				local func = tmp
				tmp = nil
				local function Caller( ... )
					--[[local arr = util.ArrayFromTable( cs.gettype( "System.Object" ), { ... } )
					for i=0, arr.Length - 1 do
						print( arr[i] )
					end]]
					local b, res = pcall( func, ... )
					if (b) then return res end
					error( "A .NET exception was thrown trying to call static function " .. method.Name .. " on " .. typ.Name .. "!" )
					util.ReportError( res )
				end
				o._methods[ method.Name ] = Caller
			end
		end
	end
	
	-- Set metatable and return
	setmetatable( o, metaType )
	return o
end

function typesystem.GetField( metatype, name, bf )
	local typ = typesystem.TypeFromMetatype( metatype )
	local field = typ:GetField( name, bf )
	if (not field) then return end
	local function Getter( obj )
		return field:GetValue( obj )
	end
	local function Setter( obj, val )
		return field:SetValue( obj, val )
	end
	return Getter, Setter
end

function typesystem.GetProperty( metatype, name, bf )
	local typ = typesystem.TypeFromMetatype( metatype )
	local prop = typ:GetProperty( name, bf )
	if (not prop) then return end
	local function Getter( obj )
		return prop:GetValue( obj, nil )
	end
	local function Setter( obj, val )
		return prop:SetValue( obj, val, nil )
	end
	return Getter, Setter
end

local metaNamespace = {}
function metaNamespace.__index( t, k )
	local global = rawget( t, "_global" )
	local fullname = global and k or (rawget( t, "_name" ) .. "." .. k)
	local assembly = rawget( t, "_assembly" )
	local typ = cs.gettype( assembly and (fullname .. ", " .. assembly) or fullname )
	if (typ) then
		local metatype = typesystem.MakeType( typ )
		rawset( t, k, metatype )
		return metatype
	end
	local o = setmetatable( {}, metaNamespace )
	o._name = fullname
	o._assembly = assembly
	rawset( t, k, o )
	return o
end

function typesystem.LoadNamespace( name, assembly, global )
	local o = setmetatable( {}, metaNamespace )
	o._name = name
	o._global = global
	o._assembly = assembly
	_G[ name ] = o
	return o
end

function typesystem.TypeFromMetatype( metatype )
	if (type( metatype ) == "table" and metatype._type) then
		return metatype._type
	else
		return metatype
	end
end

function typesystem.MakeNullableOf( metatype )
	return typesystem.MakeGeneric( cs.nullable, { metatype } )
end

function typesystem.LoadEnum( typ, name )
	cs.requestenum( "tmp", typesystem.TypeFromMetatype( typ ) )
	_G[ name ] = tmp
end

function typesystem.MakeGeneric( typ, args )
	for i=1, #args do
		args[i] = typesystem.TypeFromMetatype( args[i] )
	end
	return cs.makegenerictype( typesystem.TypeFromMetatype( typ ), args, #args )
end

function new( metatype, ... )
	local args = { ... }
	if (type( metatype ) == "table" and metatype._type) then
		return cs.new( metatype._type, args, #args )
	else
		return cs.new( metatype, args, #args )
	end
end
function newarray( metatype, sz )
	if (type( metatype ) == "table" and metatype._type) then
		return cs.newarray( metatype._type, sz )
	else
		return cs.newarray( metatype, sz )
	end
end

typesystem.LoadNamespace( "System" )
typesystem.LoadNamespace( "UnityEngine", "UnityEngine" )
typesystem.LoadNamespace( "Rust", "Assembly-CSharp", true )
typesystem.LoadNamespace( "RustFirstPass", "Assembly-CSharp-firstpass", true )