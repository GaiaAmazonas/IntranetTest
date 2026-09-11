import {afterEach,describe,expect,it,vi} from "vitest";
import {HelpdeskQueue,emptyFilters,filterKey,pagination,queueSearch,readQueueQuery,type QueuePage} from "./helpdesk-queue";
const query={...emptyFilters,page:1};
const result=(page=1,totalCount:number|null=37):QueuePage=>({page,pageSize:10,total:totalCount??-1,totalCount,hasNextPage:page<4,continuationToken:`cursor-${page+1}`,items:Array.from({length:page===4?7:10},(_,index)=>({id:`${page}-${index}`,number:`HD-${index}`,subject:"Acceso",service:"Tecnología",status:"Radicada",statusColor:null,requester:"Colaborador",responsible:null,unit:null,submittedAt:null,dueDate:null,isOverdue:false}))});
const fetcher=()=>vi.fn(async(path:string)=>result(Number(new URL(path,"https://test").searchParams.get("page"))));
afterEach(()=>vi.useRealTimers());
describe("Helpdesk queue session",()=>{
 it("loads ten rows on page 1, then 2 and 3; returning to 1 performs no HTTP",async()=>{
  const fetch=fetcher(),queue=new HelpdeskQueue(fetch);
  await queue.show(query);expect(fetch.mock.calls[0][0]).toContain("pageSize=10");
  await queue.show({...query,page:2});expect(fetch.mock.calls[1][0]).toContain("continuationToken=cursor-2");
  await queue.show({...query,page:3});await queue.show(query);
  expect(fetch).toHaveBeenCalledTimes(3);expect(queue.snapshot.data?.page).toBe(1);expect(queue.snapshot.loading).toBe(false);
 });
 it("isolates each filter combination and restores its earlier pages",async()=>{
  const fetch=fetcher(),queue=new HelpdeskQueue(fetch);await queue.show(query);
  for(const filters of [{search:"cuenta"},{serviceId:"service"},{stateId:"state"},{overdue:"true"}])await queue.show({...query,...filters});
  await queue.show(query);expect(fetch).toHaveBeenCalledTimes(5);
  expect(filterKey(query)).toContain('"pageSize":10');expect(filterKey(query)).toContain("submitted-desc");
 });
 it("debounces search for 400ms and resets to first page",async()=>{
  vi.useFakeTimers();const queue=new HelpdeskQueue(fetcher());await queue.show({...query,page:3});
  const apply=vi.fn();queue.search("a",apply);vi.advanceTimersByTime(200);queue.search("acceso",apply);
  vi.advanceTimersByTime(399);expect(apply).not.toHaveBeenCalled();vi.advanceTimersByTime(1);
  expect(apply).toHaveBeenCalledExactlyOnceWith({...query,search:"acceso",page:1});
 });
 it("ignores an obsolete response while retaining it in its own cache",async()=>{
  let resolve!:(value:QueuePage)=>void;
  const fetch=vi.fn().mockImplementationOnce(()=>new Promise<QueuePage>(done=>{resolve=done;})).mockResolvedValue(result(2));
  const queue=new HelpdeskQueue(fetch),old=queue.show(query);await queue.show({...query,search:"nuevo"});
  resolve(result());await old;expect(queue.snapshot.query.search).toBe("nuevo");expect(queue.snapshot.data?.page).toBe(2);
  await queue.show(query);expect(fetch).toHaveBeenCalledTimes(2);
 });
 it("deduplicates simultaneous reads of the same page",async()=>{
  let resolve!:(value:QueuePage)=>void;const fetch=vi.fn(()=>new Promise<QueuePage>(done=>{resolve=done;})),queue=new HelpdeskQueue(fetch);
  const first=queue.show(query),second=queue.show(query);expect(fetch).toHaveBeenCalledTimes(1);resolve(result());await Promise.all([first,second]);
  expect(queue.snapshot.loading).toBe(false);expect(queue.snapshot.data?.items).toHaveLength(10);
 });
 it("refresh forces HTTP and preserves current filters/page and unrelated cache",async()=>{
  const fetch=fetcher(),queue=new HelpdeskQueue(fetch);await queue.show(query);await queue.show({...query,serviceId:"a",page:2});
  await queue.refresh();expect(fetch).toHaveBeenCalledTimes(3);expect(queue.snapshot.query).toEqual({...query,serviceId:"a",page:2});
  await queue.show(query);expect(fetch).toHaveBeenCalledTimes(3);
 });
 it("invalidates all potentially affected combinations after mutation",async()=>{
  const fetch=fetcher(),queue=new HelpdeskQueue(fetch);await queue.show(query);await queue.show({...query,stateId:"other"});
  await queue.afterMutation();await queue.show(query);expect(fetch).toHaveBeenCalledTimes(4);
 });
 it("does not cache failures and retains previous data on failed refresh",async()=>{
  const fetch=fetcher(),queue=new HelpdeskQueue(fetch);await queue.show(query);fetch.mockRejectedValueOnce(new Error("Backend no disponible"));
  await queue.refresh();expect(queue.snapshot.error).toBe("Backend no disponible");expect(queue.snapshot.data?.items).toHaveLength(10);
  await queue.show(query);expect(fetch).toHaveBeenCalledTimes(3);expect(queue.snapshot.error).toBe("");
 });
 it("an initial failure leaves a recoverable error and no partial cache",async()=>{
  const fetch=fetcher();fetch.mockRejectedValueOnce(new Error("Error"));const queue=new HelpdeskQueue(fetch);
  await queue.show(query);expect(queue.snapshot.data).toBeNull();expect(queue.snapshot.loading).toBe(false);await queue.show(query);expect(fetch).toHaveBeenCalledTimes(2);
 });
 it("sets first/previous/next/last and trustworthy total labels",async()=>{
  const queue=new HelpdeskQueue(fetcher());await queue.show(query);expect(pagination(queue.snapshot)).toEqual({previous:false,next:true,last:4,label:"Mostrando 1–10 de 37"});
  await queue.show({...query,page:4});expect(pagination(queue.snapshot)).toEqual({previous:true,next:false,last:4,label:"Mostrando 31–37 de 37"});
 });
 it("uses hasNextPage when no reliable total is available",async()=>{
  const queue=new HelpdeskQueue(async()=>result(2,null));await queue.show({...query,page:2});
  expect(pagination(queue.snapshot)).toEqual({previous:true,next:true,last:null,label:"Página 2 · 10 resultados"});
 });
 it("represents an empty result without enabling next",async()=>{
  const queue=new HelpdeskQueue(async()=>({...result(),items:[],total:0,totalCount:0,hasNextPage:false}));await queue.show(query);
  expect(pagination(queue.snapshot)).toEqual({previous:false,next:false,last:1,label:"Mostrando 0–0 de 0"});
 });
 it("restores URL filters/page on reload but creates a new memory cache",async()=>{
  const fetch=fetcher(),next={...query,search:"cuenta & acceso",stateId:"state",page:3};
  expect(readQueueQuery(queueSearch(next).toString())).toEqual(next);
  const queue=new HelpdeskQueue(fetch);await queue.show(next);queue.dispose();await new HelpdeskQueue(fetch).show(next);expect(fetch).toHaveBeenCalledTimes(2);
  expect(readQueueQuery("page=-1").page).toBe(1);
 });
 it("does not repopulate cache from an in-flight read after a mutation",async()=>{
  let resolve!:(value:QueuePage)=>void;const fetch=vi.fn().mockImplementationOnce(()=>new Promise<QueuePage>(done=>{resolve=done;})).mockResolvedValue(result());
  const queue=new HelpdeskQueue(fetch),pending=queue.show(query);await queue.afterMutation();resolve(result(3));await pending;
  expect(queue.snapshot.data?.page).toBe(1);await queue.show(query);expect(fetch).toHaveBeenCalledTimes(2);
 });
});
