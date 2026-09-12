// eslint-disable-next-line @typescript-eslint/ban-ts-comment -- TODO: retirar al completar el tipado heredado.
// @ts-nocheck
// TODO: tipar contratos heredados de API, props y estado antes de retirar esta supresión.
/* eslint-disable react-hooks/exhaustive-deps */
import { useEffect, useMemo, useState } from 'react'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import './week14.css'

const apiRoot=import.meta.env.VITE_API_URL??`http://${window.location.hostname}:5080/api`
const number=value=>Number(value??0).toLocaleString('es-GT',{maximumFractionDigits:1})
const date=value=>value?new Date(value).toLocaleString('es-GT'):'—'

async function request(path,token){
 const response=await fetch(`${apiRoot}${path}`,{headers:{Authorization:`Bearer ${token}`}})
 const data=await response.json().catch(()=>null)
 if(!response.ok)throw new Error(data?.message??`Solicitud rechazada (${response.status}).`)
 return data
}

function query(filters){
 const params=new URLSearchParams()
 Object.entries(filters).forEach(([key,value])=>value&&params.set(key,value))
 const text=params.toString();return text?`?${text}`:''
}

async function download(path,token,name){
 const response=await fetch(`${apiRoot}${path}`,{headers:{Authorization:`Bearer ${token}`}})
 if(!response.ok)throw new Error('No fue posible generar la exportación.')
 const blob=await response.blob(),url=URL.createObjectURL(blob),link=document.createElement('a')
 link.href=url;link.download=name;link.click();URL.revokeObjectURL(url)
}

export default function Week14Analytics({session,notify}){
 const token=session.accessToken
 const [days,setDays]=useState(30),[dashboard,setDashboard]=useState(null),[events,setEvents]=useState([])
 const [options,setOptions]=useState({categories:[],users:[],zones:[]}),[selected,setSelected]=useState(null)
 const [filters,setFilters]=useState({from:'',to:'',type:'',zoneId:'',user:'',search:''})
 const loadDashboard=()=>request(`/week14/dashboard?days=${days}`,token).then(setDashboard)
 const loadHistory=()=>request(`/week14/history${query(filters)}`,token).then(setEvents)
 useEffect(()=>{Promise.all([loadDashboard(),request('/week14/filters',token).then(setOptions),loadHistory()]).catch(e=>notify(e.message))},[])
 useEffect(()=>{loadDashboard().catch(e=>notify(e.message))},[days])
 const activeFilters=useMemo(()=>Object.values(filters).filter(Boolean).length,[filters])
 const search=e=>{e.preventDefault();setSelected(null);loadHistory().catch(x=>notify(x.message))}
 const clear=()=>{setFilters({from:'',to:'',type:'',zoneId:'',user:'',search:''});setSelected(null);request('/week14/history',token).then(setEvents).catch(e=>notify(e.message))}
 const exportFile=(format)=>download(`/week14/history.${format}${query(filters)}`,token,`historial-semana14.${format}`).then(()=>notify(`Historial ${format.toUpperCase()} descargado.`)).catch(e=>notify(e.message))
 const reportFile=(path,name)=>download(`/reports/${path}${query({from:filters.from,to:filters.to,zoneId:filters.zoneId})}`,token,name).then(()=>notify('Reporte generado correctamente.')).catch(e=>notify(e.message))

 return <div className="s14">
  <header className="s14-hero"><div><p>MÓDULOS 10 Y 11 · CONSUMO Y REPORTES</p><h1>Reportes y trazabilidad</h1><span>Consolidación diaria, semanal y por sector. Incluye una muestra calibrada de prueba de 84 días para Power BI.</span></div><select value={days} onChange={e=>setDays(Number(e.target.value))}><option value="7">Últimos 7 días</option><option value="30">Últimos 30 días</option><option value="90">Últimos 90 días</option><option value="365">Último año</option></select></header>

  {dashboard&&<>
   <section className="s14-kpis"><article><span>Consumo total</span><b>{number(dashboard.totalLiters)} L</b></article><article><span>Eventos de riego</span><b>{dashboard.eventCount}</b></article><article><span>Promedio por evento</span><b>{number(dashboard.averageLiters)} L</b></article><article><span>Costo estimado</span><b>Q {number(dashboard.totalEstimatedCost)}</b></article></section>
   <section className="s14-grid"><article className="s14-card"><header><div><small>MEDIDO VS. ESTIMADO</small><h2>Origen del consumo</h2></div></header><div className="s14-sectors">{dashboard.bySource.map(item=><div key={item.source}><span><b>{item.source}</b><small>{item.events} eventos</small></span><strong>{number(item.volumeLiters)} L</strong></div>)}</div></article><article className="s14-card"><header><div><small>EFICIENCIA AGRONÓMICA</small><h2>Comparación por cultivo</h2></div></header><div className="s14-sectors">{dashboard.byCrop.map(item=><div key={item.crop}><span><b>{item.crop}</b><small>Desviación {number(item.averageDeviationPercent)}% · Q {number(item.cost)}</small></span><strong>{number(item.volumeLiters)} / {number(item.recommendedLiters)} L</strong></div>)}</div></article></section>
   <section className="s14-grid"><article className="s14-card"><header><div><small>TENDENCIA DIARIA</small><h2>Uso de agua</h2></div></header><div className="s14-chart"><ResponsiveContainer width="100%" height={260}><BarChart data={dashboard.daily} margin={{top:12,right:12,left:0,bottom:0}}><CartesianGrid strokeDasharray="3 3" vertical={false}/><XAxis dataKey="date" tickFormatter={value=>new Date(value).toLocaleDateString('es-GT',{day:'2-digit',month:'short'})}/><YAxis unit=" L"/><Tooltip formatter={value=>[`${number(value)} L`,'Consumo']} labelFormatter={value=>new Date(value).toLocaleDateString('es-GT')}/><Bar dataKey="volumeLiters" fill="#2f8f68" radius={[6,6,0,0]}/></BarChart></ResponsiveContainer></div></article>
   <article className="s14-card"><header><div><small>CONSOLIDACIÓN</small><h2>Consumo por sector</h2></div></header><div className="s14-sectors">{dashboard.bySector.map(item=><div key={item.sector}><span><b>{item.sector}</b><small>{item.events} eventos</small></span><strong>{number(item.volumeLiters)} L</strong></div>)}</div><footer>Origen: vista <code>vw_PowerBI_Consumption</code></footer></article></section>
  </>}

  <section className="s14-history"><header><div><small>BITÁCORA OPERATIVA</small><h2>Consulta avanzada</h2><p>{events.length} eventos encontrados · {activeFilters} filtros activos</p></div><div className="s14-export"><button onClick={()=>reportFile('consumption.pdf','consumo-eficiencia.pdf')}>PDF consumo</button><button onClick={()=>reportFile('irrigation.pdf','ejecuciones-riego.pdf')}>PDF riegos</button><button onClick={()=>reportFile('maintenance.pdf','mantenimiento.pdf')}>PDF mantenimiento</button><button onClick={()=>reportFile('analytics.xlsx','analitica-riego.xlsx')}>Excel analítico</button><button onClick={()=>exportFile('csv')}>CSV historial</button><button onClick={()=>exportFile('json')}>JSON historial</button></div></header>
   <form onSubmit={search} className="s14-filters"><label>Desde<input type="date" value={filters.from} onChange={e=>setFilters({...filters,from:e.target.value})}/></label><label>Hasta<input type="date" value={filters.to} onChange={e=>setFilters({...filters,to:e.target.value})}/></label><label>Categoría<select value={filters.type} onChange={e=>setFilters({...filters,type:e.target.value})}><option value="">Todas</option>{options.categories.map(x=><option key={x}>{x}</option>)}</select></label><label>Zona<select value={filters.zoneId} onChange={e=>setFilters({...filters,zoneId:e.target.value})}><option value="">Todas</option>{options.zones.map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select></label><label>Usuario<select value={filters.user} onChange={e=>setFilters({...filters,user:e.target.value})}><option value="">Todos</option>{options.users.map(x=><option key={x}>{x}</option>)}</select></label><label className="s14-search">Texto<input value={filters.search} onChange={e=>setFilters({...filters,search:e.target.value})} placeholder="Buscar en el detalle"/></label><button>Aplicar filtros</button><button type="button" className="ghost" onClick={clear}>Limpiar</button></form>
   <div className="s14-log">{events.map(item=><button key={item.id} className={selected?.id===item.id?'active':''} onClick={()=>setSelected(item)}><i className={item.severity?.toLowerCase()}/><span><b>{item.category}</b><small>{item.eventType}</small></span><span>{item.zone??'Sistema general'}<small>{item.detail}</small></span><time>{date(item.occurredAtUtc)}</time></button>)}</div>
   {selected&&<aside className="s14-detail"><button onClick={()=>setSelected(null)} aria-label="Cerrar">×</button><small>DETALLE DEL EVENTO #{selected.id}</small><h3>{selected.eventType}</h3><p>{selected.detail}</p><dl><div><dt>Fecha</dt><dd>{date(selected.occurredAtUtc)}</dd></div><div><dt>Categoría</dt><dd>{selected.category}</dd></div><div><dt>Severidad</dt><dd>{selected.severity}</dd></div><div><dt>Zona</dt><dd>{selected.zone??'Sistema general'}</dd></div><div><dt>Usuario</dt><dd>{selected.userEmail??'Proceso automático'}</dd></div></dl></aside>}
  </section>
 </div>
}
