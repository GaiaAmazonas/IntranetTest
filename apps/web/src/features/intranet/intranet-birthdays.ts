/** Próximos cumpleaños: mañana hasta 60 días; nunca fechas pasadas presentadas como próximas. */
export function upcomingBirthdaysWithin<T extends {day:number;month:number}>(items:T[],today:Date,days=60):T[]{
 const start=new Date(today.getFullYear(),today.getMonth(),today.getDate());
 const end=new Date(start);end.setDate(end.getDate()+days);
 const occurrence=(item:T)=>{const date=new Date(start.getFullYear(),item.month-1,item.day);if(date<=start)date.setFullYear(date.getFullYear()+1);return date};
 return items.filter(item=>item.month>=1&&item.month<=12&&item.day>=1&&item.day<=31&&occurrence(item)>start&&occurrence(item)<=end).sort((a,b)=>occurrence(a).getTime()-occurrence(b).getTime());
}
