import type { AuthSession } from './week2Api'

const baseUrl = import.meta.env.VITE_API_URL ?? `http://${window.location.hostname}:5080/api`
type Body = Record<string, unknown>

async function call<T>(session: AuthSession, path: string, method = 'GET', body?: Body): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: { Authorization: `Bearer ${session.accessToken}`, 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  })
  const data: unknown = response.status === 204 ? null : await response.json().catch(() => null)
  if (!response.ok) {
    const message = data && typeof data === 'object' && 'message' in data ? String(data.message) : `Solicitud rechazada (${response.status}).`
    throw new Error(message)
  }
  return data as T
}

export interface Soil { id:string; code:string; name:string; fieldCapacityPercent:number; saturationPercent:number; infiltrationMillimetersHour:number; description?:string; isActive:boolean }
export interface Stage { id:string; name:string; sequence:number; estimatedDays:number; description?:string }
export interface CropType { id:string; code:string; name:string; isActive:boolean }
export interface Crop { id:string; cropTypeId:string; cropType:string; code:string; name:string; scientificName?:string; description?:string; isActive:boolean; stages:Stage[] }
export interface Zone { id:string; name:string }
export interface Cycle { id:string; name:string; cropId:string; crop:string; irrigationZoneId:string; zone:string; currentStageId?:string; currentStage:string; sowingDate:string; expectedHarvestDate:string; actualHarvestDate?:string; areaHectares:number; plantCount:number; status:string; notes?:string }
export interface CalendarEvent { id:string; title:string; start:string; end:string; crop:string; zone:string; status:string; currentStage:string; color:string }
export interface Rotation { id:string; irrigationZoneId:string; zone:string; cropId:string; crop:string; previousCycleId?:string; previousCrop?:string; plannedStartDate:string; plannedEndDate:string; status:string; compatibilityNotes?:string }
export interface EnvironmentalEvaluation { cycleId:string; cycle:string; zone:string; temperatureCelsius?:number; ambientHumidityPercent?:number; insideAllowedSchedule:boolean; temperatureAllowed:boolean; ambientHumidityAllowed:boolean; irrigationAllowed:boolean; reasons:string[] }
export interface Requirement { id:string; cropId:string; crop:string; phenologicalStageId?:string; stage:string; soilTypeId?:string; soil:string; minimumMoisturePercent:number; targetMoisturePercent:number; maximumMoisturePercent:number; baseVolumeLiters:number; frequencyHours:number; baseDurationMinutes:number; minimumTemperatureCelsius?:number; maximumTemperatureCelsius?:number; minimumAmbientHumidityPercent?:number; maximumAmbientHumidityPercent?:number; allowedFrom?:string; allowedUntil?:string; isActive:boolean }
export interface Recommendation { cycleId:string; crop:string; zone:string; decision:string; currentMoisture?:number; suggestedMinutes:number; suggestedLiters:number; explanation:string }
export interface PlanningAlert { id:string; name:string; crop:string; zone:string; expectedHarvestDate:string; daysRemaining:number; severity:string }

export const sprint2Api = {
  soils: (s:AuthSession) => call<Soil[]>(s, '/agronomy/soil-types'),
  saveSoil: (s:AuthSession, body:Body, id?:string) => call<Soil|void>(s, `/agronomy/soil-types${id?`/${id}`:''}`, id?'PUT':'POST', body),
  deleteSoil: (s:AuthSession,id:string) => call<void>(s,`/agronomy/soil-types/${id}`,'DELETE'),
  cropTypes: (s:AuthSession) => call<CropType[]>(s,'/agronomy/crop-types'),
  saveCropType: (s:AuthSession,body:Body,id?:string) => call<CropType|void>(s,`/agronomy/crop-types${id?`/${id}`:''}`,id?'PUT':'POST',body),
  crops: (s:AuthSession) => call<Crop[]>(s,'/agronomy/crops'),
  saveCrop: (s:AuthSession,body:Body,id?:string) => call<Crop|void>(s,`/agronomy/crops${id?`/${id}`:''}`,id?'PUT':'POST',body),
  deleteCrop: (s:AuthSession,id:string) => call<void>(s,`/agronomy/crops/${id}`,'DELETE'),
  saveStage: (s:AuthSession,body:Body,id?:string) => call<Stage|void>(s,`/agronomy/stages${id?`/${id}`:''}`,id?'PUT':'POST',body),
  deleteStage: (s:AuthSession,id:string) => call<void>(s,`/agronomy/stages/${id}`,'DELETE'),
  zones: (s:AuthSession) => call<Zone[]>(s,'/manual-irrigation/zones'),
  cycles: (s:AuthSession) => call<Cycle[]>(s,'/crop-planning/cycles'),
  calendar: (s:AuthSession) => call<CalendarEvent[]>(s,'/crop-planning/calendar'),
  environmentalEvaluations: (s:AuthSession) => call<EnvironmentalEvaluation[]>(s,"/agronomy/environmental-evaluations"),
  requirements: (s:AuthSession) => call<Requirement[]>(s, '/agronomy/requirements'),
  saveRequirement: (s:AuthSession, body:Body, id?:string) => call<Requirement|void>(s, `/agronomy/requirements${id?`/${id}`:''}`, id?'PUT':'POST', body),
  deleteRequirement: (s:AuthSession, id:string) => call<void>(s, `/agronomy/requirements/${id}`, 'DELETE'),
  recommendations: (s:AuthSession) => call<Recommendation[]>(s, '/agronomy/recommendations'),
  rotations: (s:AuthSession) => call<Rotation[]>(s,"/crop-planning/rotations"),
  saveRotation: (s:AuthSession,body:Body,id?:string) => call<Rotation|void>(s,"/crop-planning/rotations"+(id?"/"+id:""),id?"PUT":"POST",body),
  deleteRotation: (s:AuthSession,id:string) => call<void>(s,"/crop-planning/rotations/"+id,"DELETE"),
  alerts: (s:AuthSession) => call<PlanningAlert[]>(s,'/crop-planning/alerts'),
  saveCycle: (s:AuthSession,body:Body,id?:string) => call<Cycle|void>(s,`/crop-planning/cycles${id?`/${id}`:''}`,id?'PUT':'POST',body),
  deleteCycle: (s:AuthSession,id:string) => call<void>(s,`/crop-planning/cycles/${id}`,'DELETE'),
}

