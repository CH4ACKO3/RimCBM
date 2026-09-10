// Technical export only: retain alpha and reduce textures to 128 pixels per tile.
const fs=require('fs'),path=require('path');
const sharp=require(process.env.SHARP_MODULE || 'sharp');
const root=path.resolve(__dirname,'..');
(async()=>{
 const preview=[];
 for(const [name,width,height,left] of [['RecoveryTable',256,256,24],['ServiceBench',384,256,308],['Autoclave',256,256,720]]) {
  const dest=path.join(root,'Textures/Things/Building/RimCBM',name+'.png');
  fs.mkdirSync(path.dirname(dest),{recursive:true});
  await sharp(path.join(root,'Art/Equipment/Source',name+'.png')).resize(width,height,{fit:'fill'}).png().toFile(dest);
  const metadata=await sharp(dest).metadata(); if(!metadata.hasAlpha)throw Error('Missing alpha: '+name);
  preview.push({input:dest,left,top:32});
 }
 const itemDest=path.join(root,'Textures/Things/Item/RimCBM/SterilizationPouch.png');
 fs.mkdirSync(path.dirname(itemDest),{recursive:true});
 await sharp(path.join(root,'Art/Equipment/SterilizationPouch.svg')).resize(128,128).png().toFile(itemDest);
 await sharp({create:{width:1000,height:320,channels:4,background:'#686d5b'}}).composite(preview).png().toFile(path.join(root,'Art/Equipment/Preview.png'));
 console.log('Exported three buildings at 128 px/tile and one pouch; retained PNG alpha.');
})().catch(e=>{console.error(e);process.exit(1)});
