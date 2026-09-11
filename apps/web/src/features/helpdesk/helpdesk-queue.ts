export const pageSize = 10;
export const queueSort = "submitted-desc";
export type QueueFilters = { search: string; serviceId: string; stateId: string; overdue: string };
export type QueueQuery = QueueFilters & { page: number };
export type QueueItem = {id:string;number:string;subject:string;service:string;status:string;statusColor:string|null;requester:string;responsible:string|null;unit:string|null;submittedAt:string|null;dueDate:string|null;isOverdue:boolean};
export type QueuePage = {total:number;totalCount:number|null;page:number;pageSize:number;items:QueueItem[];hasNextPage:boolean;continuationToken:string|null};
export type QueueSnapshot = {query:QueueQuery;data:QueuePage|null;loading:boolean;error:string};
type Entry = {data:QueuePage;loadedAt:number};
type FetchPage = (path:string, options:RequestInit)=>Promise<QueuePage>;
export const emptyFilters:QueueFilters = {search:"",serviceId:"",stateId:"",overdue:""};
export function filterKey(filters:QueueFilters) {
  return JSON.stringify({search:filters.search.trim(),service:filters.serviceId,status:filters.stateId,deadline:filters.overdue,sort:queueSort,pageSize});
}
export function readQueueQuery(search:string):QueueQuery {
  const params = new URLSearchParams(search), page = Number(params.get("page") || 1);
  return {search:(params.get("search")||"").slice(0,100),serviceId:params.get("serviceId")||"",stateId:params.get("stateId")||"",overdue:["true","false"].includes(params.get("overdue")||"")?params.get("overdue")!:"",page:Number.isSafeInteger(page)&&page>0?page:1};
}
export function queueSearch(query:QueueQuery) {
  const params = new URLSearchParams({page:String(query.page)});
  for (const name of ["search","serviceId","stateId","overdue"] as const) if(query[name]) params.set(name,query[name]);
  return params;
}
export function pagination(snapshot:QueueSnapshot) {
  const {query,data,loading}=snapshot, total=data?.totalCount;
  const last=total==null?null:Math.max(1,Math.ceil(total/pageSize));
  const count=data?.items.length??0;
  return {previous:!loading&&query.page>1,next:!loading&&!!data?.hasNextPage,last,
    label:total==null?`Página ${query.page} · ${count} resultados`:`Mostrando ${count?(query.page-1)*pageSize+1:0}–${count?(query.page-1)*pageSize+count:0} de ${total}`};
}

// Explicit invalidation, no TTL: revisiting a consulted page never performs HTTP.
// This controller belongs to one mounted page, never to module/global storage.
export class HelpdeskQueue {
  private pages = new Map<string,Map<number,Entry>>();
  private pending = new Map<string,{promise:Promise<QueuePage>;abort:AbortController}>();
  private listeners = new Set<()=>void>();
  private revision = 0;
  private debounce:ReturnType<typeof setTimeout>|undefined;
  snapshot:QueueSnapshot = {query:{...emptyFilters,page:1},data:null,loading:true,error:""};
  constructor(private fetchPage:FetchPage) {}
  subscribe=(listener:()=>void)=>{this.listeners.add(listener);return()=>{this.listeners.delete(listener);};};
  getSnapshot=()=>this.snapshot;
  private publish(value:QueueSnapshot) { this.snapshot=value;this.listeners.forEach(listener=>listener()); }
  cancelSearch() { clearTimeout(this.debounce); }
  search(value:string, apply:(query:QueueQuery)=>void) {
    this.cancelSearch();
    this.debounce=setTimeout(()=>apply({...this.snapshot.query,search:value.trim(),page:1}),400);
  }
  async show(query:QueueQuery, force=false) {
    const revision=++this.revision, key=filterKey(query), id=`${key}:${query.page}`;
    const previous=this.pages.get(key)?.get(query.page)?.data??(force&&filterKey(this.snapshot.query)===key&&this.snapshot.query.page===query.page?this.snapshot.data:null);
    if(force) {
      this.pages.delete(key);
      for(const [pendingKey,request] of this.pending) if(pendingKey.startsWith(`${key}:`)) {request.abort.abort();this.pending.delete(pendingKey);}
    }
    if(previous&&!force) {this.publish({query,data:previous,loading:false,error:""});return;}
    this.publish({query,data:previous,loading:true,error:""});
    let work=this.pending.get(id);
    if(!work) {
      const abort=new AbortController(), params=queueSearch(query);
      params.set("pageSize",String(pageSize));params.set("sort",queueSort);
      const cursor=this.pages.get(key)?.get(query.page-1)?.data.continuationToken;
      if(cursor)params.set("continuationToken",cursor);
      const promise=this.fetchPage(`/api/helpdesk/management/queue?${params}`,{cache:"no-store",signal:abort.signal});
      work={promise,abort};this.pending.set(id,work);
    }
    try {
      const data=await work.promise;
      if(this.pending.get(id)!==work)return;
      let pages=this.pages.get(key);if(!pages){pages=new Map();this.pages.set(key,pages);}
      pages.set(query.page,{data,loadedAt:Date.now()});
      if(revision===this.revision)this.publish({query,data,loading:false,error:""});
    } catch(error) {
      if(revision===this.revision)this.publish({query,data:previous,loading:false,error:error instanceof Error?error.message:"No fue posible cargar las solicitudes."});
    } finally {
      // Multiple subscribers may await the same request; cleanup after all promise reactions.
      queueMicrotask(()=>{if(this.pending.get(id)===work)this.pending.delete(id);});
    }
  }
  refresh() {return this.show(this.snapshot.query,true);}
  afterMutation() {
    this.pages.clear();
    for(const request of this.pending.values())request.abort.abort();
    this.pending.clear();
    return this.refresh();
  }
  dispose() {this.revision++;this.cancelSearch();for(const request of this.pending.values())request.abort.abort();this.pending.clear();this.pages.clear();}
}
