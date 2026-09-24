"use client";

import { useSecurity } from "@/components/security-context";
import { apiRequest } from "@/lib/api-client";
import { usePathname } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import "./visual-ambience.css";

const apiUrl=process.env.NEXT_PUBLIC_GAIA_API_URL??"https://localhost:7168";
type ActiveAmbience={id:string;theme:string;effect:number;intensity:number;primaryColor?:string;secondaryColor?:string;accentColor?:string;allowAnimation:boolean;showTopDecoration:boolean;showDecorativeBackground:boolean;promotionalText?:string;destinationUrl?:string;alternativeText?:string;desktopImageUrl?:string;mobileImageUrl?:string};

export function VisualAmbienceLayer(){
 const pathname=usePathname(),security=useSecurity(),[item,setItem]=useState<ActiveAmbience|null>(null),[celebrationKey,setCelebrationKey]=useState(0);
 const surface=pathname.startsWith("/intranet")?"intranet":pathname==="/"?null:"admincore";
 useEffect(()=>{let active=true;if(!surface||!security.user){queueMicrotask(()=>{if(active)setItem(null)});return()=>{active=false}};apiRequest<ActiveAmbience|null>(`/api/communications/active-visual-ambience?surface=${surface}`).then(value=>{if(active)setItem(value)}).catch(()=>{if(active)setItem(null)});return()=>{active=false}},[surface,security.user]);
 useEffect(()=>{const root=document.documentElement;if(item?.showTopDecoration&&surface)root.dataset.gaiaAmbienceSurface=surface;else delete root.dataset.gaiaAmbienceSurface;return()=>{delete root.dataset.gaiaAmbienceSurface}},[item?.showTopDecoration,surface]);
 const particles=useMemo(()=>Array.from({length:item?count(item.intensity):0},(_,index)=>({index,left:(index*37)%101,delay:-((index*13)%17),duration:8+((index*7)%10),size:7+((index*5)%13)})),[item]);
 if(!item)return null;
 const animated=item.allowAnimation&&item.effect!==1;
 const halloween=/(halloween|hallowin|noche de brujas)/i.test(item.theme);
 const style={"--ambience-primary":item.primaryColor||"#214d38","--ambience-secondary":item.secondaryColor||"#286b78","--ambience-accent":item.accentColor||"#f2b84b"} as React.CSSProperties;
 return <aside aria-label={`Ambientación ${item.theme}`} className={`visual-ambience effect-${item.effect} intensity-${item.intensity}${animated?" is-animated":""}${halloween?" theme-halloween":""}`} style={style}>{item.showDecorativeBackground&&<div aria-hidden="true" className="visual-ambience-particles">{particles.map(p=><i key={p.index} style={{left:`${p.left}%`,animationDelay:`${p.delay}s`,animationDuration:`${p.duration}s`,width:p.size,height:p.size}}/>)}</div>}{item.showTopDecoration&&<div aria-hidden="true" className="visual-ambience-top">{item.desktopImageUrl?<picture><source media="(max-width: 700px)" srcSet={item.mobileImageUrl?apiUrl+item.mobileImageUrl:apiUrl+item.desktopImageUrl}/><img alt="" src={apiUrl+item.desktopImageUrl}/></picture>:<><span/><span/><span/><span/><span/></>}</div>}{celebrationKey>0&&<div aria-hidden="true" className="visual-ambience-burst" key={celebrationKey}>{Array.from({length:14},(_,index)=><i key={index} style={{"--burst-angle":`${index*25.7}deg`,"--burst-distance":`${90+(index%5)*28}px`,"--burst-delay":`${(index%4)*35}ms`} as React.CSSProperties}/>)}</div>}{item.promotionalText&&(item.destinationUrl?<a className="visual-ambience-message" href={item.destinationUrl} rel="noopener noreferrer" target="_blank">{item.promotionalText}</a>:<button className="visual-ambience-message" onClick={()=>setCelebrationKey(value=>value+1)} type="button">{item.promotionalText}<span aria-hidden="true">✨</span></button>)}</aside>;
}
function count(intensity:number){return intensity===3?34:intensity===2?22:12}
