﻿#version 330 core

uniform vec3 lightPosition; // spot light position in eye space
uniform vec3 spotDirection; // spot light direction in eye space
uniform float spotCutoff; // spot light cutoff
uniform float spotExponent; // spot light exponent
uniform vec3 diffuseColor; // diffuse color of surface
uniform float constantAttenuation = 1.0;
uniform float linearAttenuation = 0;
uniform float quadraticAttenuation = 0;
uniform vec3 ambientColor = vec3(0.2, 0.2, 0.2);

// inputs from vertex shader
smooth in vec3 vEyeSpacePosition; // interpolated position in eye space
smooth in vec3 vEyeSpaceNormal; // interpolated normal in eye space

layout (location = 0) out vec4 vFragColor; // fargment shader output

void main()
{
	vec3 L = lightPosition.xyz - vEyeSpacePosition;
	float distance = length(L); // distance of point light source.
	L = normalize(L);
	vec3 D = normalize(spotDirection);
	// calculate the overlap between the spot and the light direciton
	vec3 V = -L;
	float spotEffect = dot(V, D);

	// if the spot effect is > cutoff we shade the surface.
	if (spotEffect > spotCutoff)
	{
		float diffuse = max(0, dot(vEyeSpaceNormal, L));
		spotEffect = pow(spotEffect, spotExponent);
		float attenuationAmount = 1.0 / (constantAttenuation + linearAttenuation * distance + quadraticAttenuation * distance * distance);
		diffuse *= attenuationAmount;
		if (vEyeSpaceNormal != normalize(vEyeSpaceNormal)) { diffuse = 1; }

		vFragColor = vec4(ambientColor + diffuse * diffuseColor, 1.0);
	}
	// else { discard; }
}