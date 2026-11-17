// Test harness only: the codec under test must be generated from C#, never handwritten here.
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
if(![5,7].includes(process.argv.length))throw new Error('Usage: node generated-conformance.mjs <compiled-codec-module> <C#-fixture.bin> <TS-output.bin> [<C#-rich.bin> <TS-rich.bin>]');
const {UiBinaryCodec,UiValidation,UiUtf8,UiFormatLimits}=await import(pathToFileURL(resolve(process.argv[2])).href);
assert.equal(UiFormatLimits.Bytes,4194304);
const bytes=new Uint8Array(await readFile(process.argv[3]));
const expected=new Uint8Array(Buffer.from((await readFile(new URL('./card-v1.hex',import.meta.url),'utf8')).trim(),'hex'));
assert.deepEqual(bytes,expected);
const model=UiBinaryCodec.Read(bytes);
assert.equal(model.Templates[0].Nodes[0].Name,'ação 🖌');
assert.deepEqual(new Uint8Array(UiBinaryCodec.Write(model)),bytes);
for(let size=0;size<bytes.length;size++)assert.throws(()=>UiBinaryCodec.Read(bytes.slice(0,size)));
const trailing=new Uint8Array(bytes.length+1);trailing.set(bytes);assert.throws(()=>UiBinaryCodec.Read(trailing));
for(const [offset,value]of [[5,2],[56,0xc0],[86,2]]){const bad=bytes.slice();bad[offset]=value;assert.throws(()=>UiBinaryCodec.Read(bad));}
for(const number of [NaN,Infinity,-Infinity,1e40])assert.throws(()=>UiValidation.Finite(number));
for(const number of [0.5,NaN,Infinity,2147483648])assert.throws(()=>UiValidation.Integer(number,1,2147483647));
assert.throws(()=>UiUtf8.Length('bad\ud800'));
assert.throws(()=>UiUtf8.Length('bad\udc00'));
model.Templates[0].Nodes[0].Id=1.5;assert.throws(()=>UiBinaryCodec.Write(model));
model.Templates[0].Nodes[0].Id=1;
await writeFile(process.argv[4],new Uint8Array(UiBinaryCodec.Write(model)));
console.log('PASS: generated TypeScript reads C# bytes and writes identical bytes; malformed data rejected.');

if(process.argv.length===7){
 const richBytes=new Uint8Array(await readFile(process.argv[5]));
 const rich=UiBinaryCodec.Read(richBytes),node=rich.Templates[0].Nodes[0];
 assert.equal(node.Components[0].Properties[0].Text,'\ufeffliteral');
 assert.equal(node.Components[0].Properties[1].Boolean,true);
 assert.equal(node.Components[0].Properties[2].Integer,3);
 assert.equal(node.Components[0].Properties[3].Color,0xff2200ff);
 assert.equal(node.Components[1].Properties[0].Text,'cover');
 assert.equal(node.Components[2].Properties[0].Text,'open');
 assert.equal(node.Components[2].Properties[1].Text,'name');
 assert.equal(rich.Templates[0].Nodes[1].ParentId,1);
 assert(Object.is(rich.Templates[0].Nodes[1].X,-0));
 const output=new Uint8Array(UiBinaryCodec.Write(rich));assert.deepEqual(output,richBytes);
 await writeFile(process.argv[6],output);
 console.log('PASS: all remaining property tags, parent links, literal BOM, unsigned colors and negative zero agree across runtimes.');
}

// Ownership is observable even with GC: replacement disposes the old subtree and clears its fields.
const owned=UiBinaryCodec.Read(bytes),oldTemplate=owned.Templates[0],oldNode=oldTemplate.Nodes[0],oldComponent=oldNode.Components[0];
owned.Templates=owned.Templates;assert.equal(owned.Templates[0],oldTemplate);
owned.Templates=[null,null];
assert.equal(oldTemplate.Nodes,null);assert.equal(oldNode.Components,null);assert.equal(oldComponent.Properties,null);
owned.dispose();owned.dispose();assert.equal(owned.Templates,null);assert.equal(owned.Metrics,null);
for(let size=20;size<bytes.length;size++){
 const cut=bytes.slice(0,size);new DataView(cut.buffer).setUint32(12,size-16,true);
 assert.throws(()=>UiBinaryCodec.Read(cut),error=>error.constructor.name==='FormatException');
}
model.dispose();model.dispose();
console.log('PASS: generated cleanup clears owners, tolerates partial trees and repeated disposal, and preserves malformed-input failures.');
