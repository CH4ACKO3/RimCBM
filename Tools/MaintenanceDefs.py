"""Generate recovery equipment and update the stable maintenance recipes."""
from xml.etree.ElementTree import Element, SubElement as E, ElementTree, parse, indent, tostring

def build(root):
 def n(p,k,v=None,**a):
  x=E(p,k,a)
  if v is not None:x.text=str(v)
  return x
 def save(x,path):
  p=root/path;p.parent.mkdir(parents=True,exist_ok=True);indent(x,space='  ')
  p.write_bytes(tostring(x,encoding='utf-8',xml_declaration=True)+b'\n')
 defs=Element('Defs');zh=Element('LanguageData')
 equipment=[('ServiceBench','implant service bench','植入体整备台',(3,2),200,180,5,'A powered bench for cleaning, repairing and dismantling recovered implants. Repair requires a clean implant and leaves it unsterile.','用于清洗、维修和拆解回收植入体的电动工作台。维修前需清洗，维修后仍需灭菌。'),('RecoveryTable','implant recovery table','植入体回收台',(2,2),120,140,3,'Dissect corpses to recover only implants actually present. Medical skill reduces extraction damage and burnout risk. The corpse remains.','解剖遗体，回收其实际携带的植入体。医疗技能降低损伤和烧毁概率；操作后保留遗体。'),('Autoclave','implant autoclave','植入体高压灭菌釜',(2,2),1200,220,6,'Load one clean implant with a sterilization pouch. Runs unattended for six in-game hours while powered. Power loss pauses the cycle; contents and progress are saved. Output remains sterile until repair or contamination.','装入一个清洁植入体和一只灭菌袋后，自动运行六个游戏小时。断电暂停，内容和进度可保存。完成后保持无菌，直到维修或污染。')]
 for key,label,cn,size,power,steel,components,desc,cn_desc in equipment:
  t=n(defs,'ThingDef',ParentName='BuildingBase')
  for k,v in dict(defName='RimCBM_'+key,label=label,description=desc,thingClass='RimCBM.Building_CBMAutoclave' if key=='Autoclave' else 'Building_WorkTable',size=f'({size[0]},{size[1]})',designationCategory='Production',passability='PassThroughOnly',pathCost=50,hasInteractionCell='true',interactionCellOffset='(0,0,-1)',tickerType='Rare',rotatable='true').items():n(t,k,v)
  g=n(t,'graphicData');n(g,'texPath','Things/Building/RimCBM/'+key);n(g,'graphicClass','Graphic_Single');n(g,'drawSize',f'({size[0]},{size[1]})');n(g,'shaderType','Cutout')
  s=n(t,'statBases')
  for k,v in dict(MaxHitPoints=200,WorkToBuild=4500,Mass=60,Flammability=.3,WorkTableWorkSpeedFactor=1).items():n(s,k,v)
  cost=n(t,'costList');n(cost,'Steel',steel);n(cost,'ComponentIndustrial',components)
  n(n(t,'researchPrerequisites'),'li','RimCBM_Reconditioning')
  n(n(t,'inspectorTabs'),'li','ITab_Bills')
  b=n(t,'building');n(b,'unpoweredWorkTableWorkSpeedFactor',0);n(b,'deconstructible','true')
  comps=n(t,'comps');c=n(comps,'li',Class='CompProperties_Power');n(c,'compClass','CompPowerTrader');n(c,'basePowerConsumption',power)
  n(comps,'li',Class='CompProperties_Flickable');n(comps,'li',Class='CompProperties_Breakdownable')
  if key=='Autoclave':
   e=n(n(t,'modExtensions'),'li',Class='RimCBM.CBMAutoclaveExtension');n(e,'cycleTicks',15000)
  n(zh,'RimCBM_'+key+'.label',cn);n(zh,'RimCBM_'+key+'.description',cn_desc)
 t=n(defs,'ThingDef',ParentName='ResourceBase')
 for k,v in dict(defName='RimCBM_SterilizationPouch',label='sterilization pouch',description='A disposable sterilization wrap and process indicator, consumed when loading an implant autoclave.',stackLimit=75).items():n(t,k,v)
 g=n(t,'graphicData');n(g,'texPath','Things/Item/RimCBM/SterilizationPouch');n(g,'graphicClass','Graphic_Single')
 s=n(t,'statBases');n(s,'MaxHitPoints',40);n(s,'Mass',.03);n(s,'MarketValue',3);n(s,'Flammability',1)
 n(n(t,'thingCategories'),'li','Manufactured')
 category=n(defs,'ThingCategoryDef');n(category,'defName','RimCBM_Implants');n(category,'label','implants');n(category,'parent','Manufactured')
 prototypes=parse(root/'Defs/CBM_Prototypes.xml').getroot()
 for item in prototypes.findall('ThingDef'):
  if item.findtext('defName','').endswith('Item'):
   cats=item.find('thingCategories')
   if cats is None:cats=n(item,'thingCategories')
   n(cats,'li','RimCBM_Implants')
 save(prototypes,'Defs/CBM_Prototypes.xml')
 n(zh,'RimCBM_SterilizationPouch.label','灭菌袋');n(zh,'RimCBM_SterilizationPouch.description','一次性灭菌包材和指示条，在高压灭菌釜装料时消耗。')
 r=n(defs,'ResearchProjectDef')
 for k,v in dict(defName='RimCBM_Reconditioning',label='implant reconditioning',description='Specialized recovery, cleaning, repair and autoclave equipment.',baseCost=1200,techLevel='Industrial',researchViewX=1,researchViewY=1,tab='RimCBM').items():n(r,k,v)
 n(n(r,'prerequisites'),'li','RimCBM_Integration')
 rz=Element('LanguageData');n(rz,'RimCBM_Reconditioning.label','植入体回收整备');n(rz,'RimCBM_Reconditioning.description','解锁专用回收、清洗、维修及高压灭菌设备。')
 save(rz,'Languages/ChineseSimplified/DefInjected/ResearchProjectDef/Maintenance.xml')
 maintenance=parse(root/'Defs/CBM_Maintenance.xml').getroot()
 for recipe in maintenance:
  kind=recipe.findtext('modExtensions/li/service')
  recipe.find('recipeUsers/li').text='RimCBM_Autoclave' if kind=='Sterilize' else 'RimCBM_ServiceBench'
  if kind=='Sterilize':recipe.find('workAmount').text='300'
  if kind=='Clean':
   i=n(recipe.find('ingredients'),'li');n(n(n(i,'filter'),'thingDefs'),'li','Cloth');n(i,'count',1);n(recipe.find('fixedIngredientFilter/thingDefs'),'li','Cloth')
  if kind=='Sterilize':
   for li in recipe.findall('.//thingDefs/li'):
    if li.text=='MedicineIndustrial':li.text='RimCBM_SterilizationPouch'
 item_names=[x.text for x in maintenance[0].findall('ingredients/li/filter/thingDefs/li') if x.text.startswith('RimCBM_')]
 service_zh=parse(root/'Languages/ChineseSimplified/DefInjected/RecipeDef/CBM_Maintenance.xml').getroot()
 for key,label,cn,bench,work in [('Recover','recover implants from corpse','从遗体回收植入体','RecoveryTable',6000),('Recycle','dismantle burnt-out implant','拆解烧毁植入体','ServiceBench',1800)]:
  r=n(maintenance,'RecipeDef')
  for k,v in dict(defName='RimCBM_'+key,label=label,description=label,jobString=label+'.',workerClass='RimCBM.Recipe_ServiceCBM',workAmount=work,workSpeedStat='GeneralLaborSpeed',workSkill='Medicine' if key=='Recover' else 'Crafting').items():n(r,k,v)
  n(n(r,'recipeUsers'),'li','RimCBM_'+bench)
  n(n(r,'skillRequirements'),'Medicine' if key=='Recover' else 'Crafting',4)
  e=n(n(r,'modExtensions'),'li',Class='RimCBM.CBMServiceExtension');n(e,'service',key)
  ing=n(n(r,'ingredients'),'li');f=n(ing,'filter');fixed=n(r,'fixedIngredientFilter')
  for parent in (f,fixed):
   if key=='Recover':n(n(parent,'categories'),'li','CorpsesHumanlike')
   else:
    things=n(parent,'thingDefs')
    for name in item_names:n(things,'li',name)
  n(ing,'count',1)
  for suffix,text in [('label',cn),('description',cn),('jobString','正在'+cn+'。')]:n(service_zh,'RimCBM_'+key+'.'+suffix,text)
 r=n(maintenance,'RecipeDef')
 for k,v in dict(defName='RimCBM_MakePouches',label='make sterilization pouches x5',description='Make five disposable sterilization pouches from cloth and neutroamine.',jobString='making sterilization pouches.',workAmount=600,workSpeedStat='GeneralLaborSpeed',workSkill='Crafting').items():n(r,k,v)
 n(n(r,'recipeUsers'),'li','RimCBM_ServiceBench');n(n(r,'products'),'RimCBM_SterilizationPouch',5)
 ingredients=n(r,'ingredients');fixed=n(n(r,'fixedIngredientFilter'),'thingDefs')
 for name,count in [('Cloth',2),('Neutroamine',1)]:
  i=n(ingredients,'li');n(n(n(i,'filter'),'thingDefs'),'li',name);n(i,'count',count);n(fixed,'li',name)
 for suffix,text in [('label','制作灭菌袋 ×5'),('description','用布料和中性胺制作五只一次性灭菌袋。'),('jobString','正在制作灭菌袋。')]:n(service_zh,'RimCBM_MakePouches.'+suffix,text)
 for r in maintenance:
  if r.findtext('modExtensions/li/service') not in ('Clean','Repair','Sterilize','Recycle'):continue
  for f in [r.find('ingredients/li/filter'),r.find('fixedIngredientFilter')]:
   things=f.find('thingDefs')
   for li in list(things):
    if li.text in item_names:things.remove(li)
   if len(things)==0:f.remove(things)
   n(n(f,'categories'),'li','RimCBM_Implants')
 save(maintenance,'Defs/CBM_Maintenance.xml');save(service_zh,'Languages/ChineseSimplified/DefInjected/RecipeDef/CBM_Maintenance.xml')
 for suffix,worktype,benches in [('Service','Crafting',['ServiceBench','Autoclave']),('Recovery','Doctor',['RecoveryTable'])]:
  w=n(defs,'WorkGiverDef')
  for k,v in dict(defName='RimCBM_'+suffix,giverClass='WorkGiver_DoBill',workType=worktype,priorityInType=90,label='service implants' if suffix=='Service' else 'recover implants',verb='service',gerund='servicing implants').items():n(w,k,v)
  for bench in benches:n(w.find('fixedBillGiverDefs') if w.find('fixedBillGiverDefs') is not None else n(w,'fixedBillGiverDefs'),'li','RimCBM_'+bench)
 category_zh=Element('LanguageData');n(category_zh,'RimCBM_Implants.label','植入体');save(category_zh,'Languages/ChineseSimplified/DefInjected/ThingCategoryDef/Maintenance.xml')
 work_zh=Element('LanguageData')
 for key,label in [('Service','整备植入体'),('Recovery','回收植入体')]:
  for field,value in [('label',label),('verb',label),('gerund','正在'+label)]:n(work_zh,'RimCBM_'+key+'.'+field,value)
 save(work_zh,'Languages/ChineseSimplified/DefInjected/WorkGiverDef/Maintenance.xml')
 save(defs,'Defs/CBM_MaintenanceEquipment.xml');save(zh,'Languages/ChineseSimplified/DefInjected/ThingDef/Maintenance.xml')
 for lang,cycle,empty in [('English','{0}: {1} remaining (power and intact machinery required).','Empty. Add a sterilization bill to load one implant.'),('ChineseSimplified','{0}：剩余 {1}（需要通电且设备完好）。','空闲。在清单中添加灭菌任务以装入植入体。')]:
  data=Element('LanguageData');n(data,'RC_AutoclaveCycle',cycle);n(data,'RC_AutoclaveEmpty',empty);n(data,'RC_AutoclaveEject','取出内容' if lang=='ChineseSimplified' else 'Eject contents');n(data,'RC_AutoclaveEjectHelp','中途取出仍为未灭菌，已消耗的灭菌袋不返还。' if lang=='ChineseSimplified' else 'An incomplete cycle leaves the implant unsterile. Consumed pouches are not refunded.');save(data,f'Languages/{lang}/Keyed/Maintenance.xml')
