"use client";

import { useEffect, useRef, useState } from "react";

type Player={getCurrentTime:()=>number;getDuration:()=>number;getPlayerState:()=>number;destroy:()=>void};
type YouTubeWindow=Window&{YT?:{Player:new(element:HTMLElement,options:{events:{onError:()=>void}})=>Player};onYouTubeIframeAPIReady?:()=>void};
let sdk:Promise<void>|null=null;
function loadYouTube(){
  const target=window as YouTubeWindow;if(target.YT?.Player)return Promise.resolve();
  if(!sdk)sdk=new Promise<void>((resolve,reject)=>{
    const prior=target.onYouTubeIframeAPIReady;target.onYouTubeIframeAPIReady=()=>{prior?.();resolve()};
    const script=document.createElement("script");script.src="https://www.youtube.com/iframe_api";script.async=true;
    const timeout=setTimeout(()=>{sdk=null;reject(new Error("El reproductor de YouTube no pudo cargarse."))},15000);
    script.onerror=()=>{clearTimeout(timeout);sdk=null;reject(new Error("El reproductor de YouTube no pudo cargarse."))};
    const ready=target.onYouTubeIframeAPIReady;target.onYouTubeIframeAPIReady=()=>{clearTimeout(timeout);ready?.()};document.head.appendChild(script);
  });return sdk;
}

export function TrainingVideo({source,embed,title,onProgress}:{source:string|null;embed:string|null;title:string;onProgress:(percentage:number,seconds:number)=>void}) {
  const host=useRef<HTMLDivElement>(null),callback=useRef(onProgress),[error,setError]=useState("");
  useEffect(()=>{callback.current=onProgress},[onProgress]);
  useEffect(()=>{
    if(!embed||!host.current)return;let alive=true,player:Player|null=null,timer:ReturnType<typeof setInterval>|null=null;const watched=new Set<number>();let previous:number|null=null;
    const frame=document.createElement("iframe");frame.className="training-video";frame.src=`${embed}?enablejsapi=1&origin=${encodeURIComponent(window.location.origin)}`;frame.title=title;frame.allow="accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture";frame.allowFullscreen=true;host.current.replaceChildren(frame);
    void loadYouTube().then(()=>{
      if(!alive)return;const target=window as YouTubeWindow;
      player=new target.YT!.Player(frame,{events:{onError:()=>{if(alive)setError("YouTube no permite reproducir este video. Comprueba que sea público y admita inserción.")}}});
      timer=setInterval(()=>{
        if(!player?.getCurrentTime)return;const current=player.getCurrentTime(),duration=player.getDuration();
        if([0,1].includes(player.getPlayerState())&&previous!==null&&current>=previous&&current-previous<2&&current>previous){for(let n=Math.floor(previous);n<Math.ceil(current);n++)watched.add(n);}
        previous=current;if(duration>0)callback.current(Math.min(100,watched.size*100/duration),watched.size);
      },500);
    }).catch(e=>{if(alive)setError(e instanceof Error?e.message:"El video no pudo cargarse.")});
    return()=>{alive=false;if(timer)clearInterval(timer);player?.destroy();frame.remove()};
  },[embed,title]);
  if(embed)return <div><div ref={host}/>{error&&<p role="alert">{error}</p>}</div>;
  return <div>{source?<video className="training-video" controls preload="metadata" src={source} onError={()=>setError("No fue posible reproducir el archivo. Prueba descargarlo o consulta al responsable.")} onTimeUpdate={e=>{const video=e.currentTarget;let seconds=0;for(let i=0;i<video.played.length;i++)seconds+=video.played.end(i)-video.played.start(i);if(video.duration>0)callback.current(Math.min(100,seconds*100/video.duration),Math.floor(seconds))}}/>:<p>El video no tiene una fuente disponible.</p>}{error&&<p role="alert">{error}</p>}{source&&<a className="text-sm underline" href={source+(source.includes("?")?"&":"?")+"download=true"} target="_blank" rel="noopener noreferrer">Descargar video</a>}</div>;
}
